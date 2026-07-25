# Automated deploy — one-time VM setup

This wires up the "Deploy to Production" GitHub Actions workflow
(`.github/workflows/deploy.yml`) so it can push builds to the Hetzner VM
without holding your root key (`~/.ssh/hetzner`). It creates a separate,
restricted `deploy` user whose SSH key can do exactly one thing — run
`deploy-recipehelper.sh` — nothing else. A leaked CI secret can overwrite
this app and restart this service; it can't get a shell, read other files,
or touch anything else on the box.

SSH goes over Tailscale so the Hetzner firewall port 22 restriction stays
in place — GitHub Actions runners join your Tailscale network for the
duration of each run, then leave.

Do this once. All steps below are commands you run yourself (SSH to the VM
as root, plus a few clicks on tailscale.com and GitHub).

## 1. Deploy SSH key — already done

The key pair has been generated and `DEPLOY_SSH_KEY` is already set as a
GitHub Actions secret in the `production` environment. The public key you
need to paste on the VM in step 3 is:

```
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIJ8SYDxxjgGcFpuaFAp4V+w8gkW4G1Ri5bH4aGNrKlaQ github-actions-deploy
```

## 2. Install Tailscale on the VM

```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57
```

On the VM:

```bash
curl -fsSL https://tailscale.com/install.sh | sh
tailscale up
```

Follow the auth URL it prints to add the VM to your Tailscale network. Once
connected, note the machine's Tailscale hostname (visible at tailscale.com/admin
→ Machines, e.g. `hetzner-vm`).

## 3. Create a Tailscale OIDC (federated identity) client for GitHub Actions

The workflow authenticates to Tailscale using GitHub's OIDC token — no
long-lived client secret is stored in GitHub. At
[tailscale.com/admin → Settings → OAuth Clients](https://tailscale.com/admin/settings/oauth):

1. **New OAuth client** → name it `github-actions-deploy`
2. Scopes: check **Auth Keys → Write** (federated identities need write
   access to register ephemeral nodes, not just read)
3. Under Tags, add `tag:ci` (create it first if needed — go to Access
   Controls and add `"tag:ci": []` to the `tagOwners` section). This must
   match the `tags:` value the workflow passes to the action — Tailscale
   requires every tag the workflow requests to already be on the client.
4. Configure it as a federated identity trusting GitHub Actions' OIDC
   issuer, and note the **Client ID** and **audience** value it gives you
   (audience looks like `api.tailscale.com/<tailnet-id>`) — no client
   secret is generated for this flow.

Add these as GitHub secrets in the `production` environment
(repo → Settings → Environments → production → Add secret):

| Secret name | Value |
|---|---|
| `TS_OAUTH_CLIENT_ID` | Client ID from above |
| `DEPLOY_HOST` | Tailscale hostname of the VM (e.g. `hetzner-vm`) |

The `audience` value is hardcoded directly in `.github/workflows/deploy.yml`
(not a secret, since it isn't sensitive).

## 4. Create the restricted deploy user on the VM

```bash
# Copy the deploy script to the VM
scp -i ~/.ssh/hetzner deploy/remote/deploy-recipehelper.sh root@178.105.73.57:/usr/local/bin/deploy-recipehelper.sh

ssh -i ~/.ssh/hetzner root@178.105.73.57
```

On the VM:

```bash
# Script must be root-owned and not writable by `deploy` — it runs as root
# via sudo, so if `deploy` could edit it, the restriction is worthless.
chown root:root /usr/local/bin/deploy-recipehelper.sh
chmod 755 /usr/local/bin/deploy-recipehelper.sh

# No password, no interactive shell.
useradd --create-home --shell /usr/sbin/nologin deploy

# Let `deploy` run *only* that exact script as root, no password prompt.
echo 'deploy ALL=(root) NOPASSWD: /usr/local/bin/deploy-recipehelper.sh' \
    > /etc/sudoers.d/recipehelper-deploy
chmod 440 /etc/sudoers.d/recipehelper-deploy

# Restrict the deploy user's key to that one forced command.
# Paste the public key from step 1 exactly as shown.
mkdir -p /home/deploy/.ssh
cat <<'KEY' > /home/deploy/.ssh/authorized_keys
command="sudo /usr/local/bin/deploy-recipehelper.sh",no-port-forwarding,no-agent-forwarding,no-X11-forwarding,no-pty ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIJ8SYDxxjgGcFpuaFAp4V+w8gkW4G1Ri5bH4aGNrKlaQ github-actions-deploy
KEY
chown -R deploy:deploy /home/deploy/.ssh
chmod 700 /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys
```

## 5. Place production config on the VM

The deploy script copies `appsettings.Production.json` from `/etc/recipehelper/`
into the app directory after each deploy, so the secrets file never needs to
live in the GitHub repo or as a GitHub secret.

On the VM (one time):

```bash
mkdir -p /etc/recipehelper
```

Then from your laptop, copy the file over:

```bash
scp -i ~/.ssh/hetzner RecipeHelper/appsettings.Production.json root@178.105.73.57:/etc/recipehelper/appsettings.Production.json
```

The deploy script will pick it up from there on every subsequent deploy.

## 6. Run it

Actions tab → **Deploy to Production** → **Run workflow** → optionally
type a branch name (e.g. a PR branch) instead of the `main` default →
**Run workflow**. Works from the GitHub mobile app.

Since this deploys to the live single-user app, remember: whatever branch
you deploy is what's live until the next deploy. If you deploy a PR branch
and decide not to merge it, re-run the workflow with `main`.

## Rotating or revoking access

- **Cut off CI entirely:** delete `/home/deploy/.ssh/authorized_keys` (or the
  whole `deploy` user) on the VM, or remove the `TS_OAUTH_CLIENT_ID` secret
  from GitHub / delete the OAuth client on tailscale.com. The root key and
  manual `deploy/deploy.sh` path are unaffected.
- **Rotate the deploy key:** generate a new keypair, update `DEPLOY_SSH_KEY` in GitHub,
  and replace the public key in `/home/deploy/.ssh/authorized_keys` on the VM.
- **Rotate Tailscale credentials:** delete the OAuth client on tailscale.com and
  create a new one (with `tag:ci` and Auth Keys write scope), then update
  `TS_OAUTH_CLIENT_ID` in GitHub and the `audience` value in the workflow file.
