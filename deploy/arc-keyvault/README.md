# Secrets off the VM: Azure Arc managed identity + Key Vault

**Goal:** the Hetzner VM keeps no secrets on disk. The Azure Arc agent gives the VM a
managed identity (free; Azure rotates its tokens). The app uses that identity to:

- read its 4 API secrets from Key Vault `kv-recipehelper-gwc` at startup
- connect to SQL with `Authentication=Active Directory Managed Identity`, so no password is needed
- write to blob storage and App Insights, as `sp-recipe-helper-p` does today

**Code side** (`Utility/AzureCredentials.cs`, `Program.cs`): when `AzureAd:ClientSecret` is
set, the app uses the service principal exactly as before. When it isn't set, the app uses
the managed identity. Key Vault is only read when `KeyVault:Uri` is set. So the code can
ship **before** any of this is done, and nothing changes until step 6 swaps the config.

**Fixed values:** subscription `a4dd9a73-b1d2-4b67-94f0-a7e7fe0bacf7`, tenant
`0a35fca2-7c73-4e78-8923-0cc50fd94ea1`, RG `rg-recipehelper-germanycentral`, region
`germanywestcentral`, Arc machine name `vm-recipehelper-hetzner`.

Run the `az` commands locally (Git Bash, after `az login`). Run the VM steps over SSH as root.

```bash
S=a4dd9a73-b1d2-4b67-94f0-a7e7fe0bacf7
RG=rg-recipehelper-germanycentral
```

## 1. Onboard the VM to Azure Arc (on the VM)

```bash
wget https://gbl.his.arc.azure.com/azcmagent-linux -O /tmp/install_linux_azcmagent.sh
bash /tmp/install_linux_azcmagent.sh
azcmagent connect \
  --resource-group rg-recipehelper-germanycentral \
  --tenant-id 0a35fca2-7c73-4e78-8923-0cc50fd94ea1 \
  --subscription-id a4dd9a73-b1d2-4b67-94f0-a7e7fe0bacf7 \
  --location germanywestcentral \
  --resource-name vm-recipehelper-hetzner
# prints a device-code login: open the URL on any device and sign in
azcmagent show    # Agent Status should be "Connected"
```

The app runs as `www-data`. Reading the Arc identity's token requires the `himds` group:

```bash
usermod -aG himds www-data
```

(This takes effect at the next service restart, in step 6.)

## 2. Create the Key Vault and grant access (local)

```bash
az extension add --name connectedmachine --upgrade
MI=$(az connectedmachine show -g $RG -n vm-recipehelper-hetzner --subscription $S --query identity.principalId -o tsv)
echo $MI   # must be a GUID

az keyvault create -n kv-recipehelper-gwc -g $RG -l germanywestcentral --subscription $S \
  --enable-rbac-authorization true
KV=$(az keyvault show -n kv-recipehelper-gwc --subscription $S --query id -o tsv)

# The VM reads secrets. You write them.
az role assignment create --assignee-object-id $MI --assignee-principal-type ServicePrincipal \
  --role "Key Vault Secrets User" --scope $KV
az role assignment create --assignee "$(az ad signed-in-user show --query id -o tsv)" \
  --role "Key Vault Secrets Officer" --scope $KV

# Same storage and App Insights roles sp-recipe-helper-p has
az role assignment create --assignee-object-id $MI --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" \
  --scope "$(az storage account show -n sarecipehelper -g $RG --subscription $S --query id -o tsv)"
az role assignment create --assignee-object-id $MI --assignee-principal-type ServicePrincipal \
  --role "Monitoring Metrics Publisher" \
  --scope "$(az resource show -g $RG -n insights-recipehelper-germanycentral --resource-type microsoft.insights/components --subscription $S --query id -o tsv)"
```

## 3. Put the secrets in the vault (local)

New role assignments can take a few minutes to apply. Then run:

```bash
python deploy/arc-keyvault/push-secrets.py
```

The script copies `OpenAI:ApiKey`, `Spoonacular:apiKey`, `Kroger:clientSecret` and
`Oauth:ClientSecret` from your local `appsettings.Production.json`. It prints the key names
only, never the values.

## 4. Give the VM's identity a SQL login

Connect to the `recipehelper` database as a member of the Entra admin group
`gAZRecipeSqlAdmin`. Use the Azure portal Query editor, Azure Data Studio, or `sqlcmd -G`.
Then run:

```sql
CREATE USER [vm-recipehelper-hetzner] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [vm-recipehelper-hetzner];
ALTER ROLE db_datawriter ADD MEMBER [vm-recipehelper-hetzner];
ALTER ROLE db_ddladmin   ADD MEMBER [vm-recipehelper-hetzner];
```

These are the same roles `sp-recipe-helper-p` has. The SQL firewall already allows the VM's IP.

## 5. Ship the code

Merge the Key Vault PR. It has no effect yet, because the VM config still has the client
secret and no `KeyVault:Uri`.

## 6. Swap the VM config (on the VM)

```bash
cp -p /etc/recipehelper/appsettings.Production.json /etc/recipehelper/appsettings.Production.json.bak-pre-arc
nano /etc/recipehelper/appsettings.Production.json
```

Edit the file:

- **Add** `"KeyVault": { "Uri": "https://kv-recipehelper-gwc.vault.azure.net/" }`.
- **Replace** `ConnectionString` with
  `Server=tcp:sql-recipe-helper.database.windows.net,1433;Database=recipehelper;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;`
- **Delete** the whole `AzureAd` section.
- **Delete** the 4 secret values: `OpenAI:ApiKey`, `Spoonacular:apiKey`, `Kroger:clientSecret`
  and `Oauth:ClientSecret`. Remove the keys entirely; don't leave them as empty strings,
  because those would be blank in config.
- **Delete** the whole `OpenTelemetry:Otlp` section. It's left over from Grafana and its
  `Headers` holds an old Grafana token.

Then restart and check:

```bash
systemctl restart recipehelper
systemctl status recipehelper --no-pager | head -5
journalctl -u recipehelper -n 50 --no-pager | grep -iE "fail|exception|listening"
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/
```

**Rollback:** restore the backup and restart.

```bash
cp -p /etc/recipehelper/appsettings.Production.json.bak-pre-arc /etc/recipehelper/appsettings.Production.json
systemctl restart recipehelper
```

The deploy script copies `/etc/recipehelper/appsettings.Production.json` into the app on
every deploy, so the new config survives CI deploys. `deploy/deploy.sh` ships your *local*
`RecipeHelper/appsettings.Production.json` instead, so make the same edits there too.

## 7. Clean up once it's verified

Check that recipes load, a photo import works (OpenAI + blob), and a cart add works (Kroger).
Then:

- **Delete the old client secret** on `sp-recipe-helper-p`: `az ad app credential list --id 3e54accb-87f2-4f61-9732-9d01bf5c669d`, then `az ad app credential delete ...`. It has been sitting on disk, so treat it as exposed. Keep the SP itself only if local dev still needs it.
- **Delete** the VM backup file `appsettings.Production.json.bak-pre-arc`, since it contains the old secrets.
- **Optional:** rotate the 4 API keys, since they lived on disk too. Update the vault with `az keyvault secret set`, and the app picks the new values up on its next restart.
