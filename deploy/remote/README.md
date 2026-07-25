# Automated deploy — one-time VM setup

This wires up the "Deploy to Production" GitHub Actions workflow
(`.github/workflows/deploy.yml`) so it can push builds to the Hetzner VM
without holding your root key (`~/.ssh/hetzner`). It creates a separate,
restricted `deploy` user whose SSH key can do exactly one thing — run
`deploy-recipehelper.sh` — nothing else. A leaked CI secret can overwrite
this app and restart this service; it can't get a shell, read other files,
or touch anything else on the box.

Do this once. Steps 1–2 are commands you run yourself; nothing here is
run automatically until you've added the GitHub secret in step 3.

## 1. Generate a dedicated CI keypair (on your laptop — do not reuse `~/.ssh/hetzner`)

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ./ci_deploy_key -N ""
```

This creates `ci_deploy_key` (private — goes to GitHub) and
`ci_deploy_key.pub` (public — goes to the VM).

## 2. Create the restricted user on the VM (as root)

```bash
# Copy the deploy script over first
scp -i ~/.ssh/hetzner deploy/remote/deploy-recipehelper.sh root@178.105.73.57:/usr/local/bin/deploy-recipehelper.sh

ssh -i ~/.ssh/hetzner root@178.105.73.57
```

Then, on the VM:

```bash
# Script must be root-owned and not writable by `deploy` — it will run as
# root via sudo, so if `deploy` could edit it, the restriction is worthless.
chown root:root /usr/local/bin/deploy-recipehelper.sh
chmod 755 /usr/local/bin/deploy-recipehelper.sh

# No password, no interactive shell — the forced command below is the only
# thing this account can ever do over SSH.
useradd --create-home --shell /usr/sbin/nologin deploy

# Let `deploy` run *only* that exact script as root, no password prompt.
echo 'deploy ALL=(root) NOPASSWD: /usr/local/bin/deploy-recipehelper.sh' \
    > /etc/sudoers.d/recipehelper-deploy
chmod 440 /etc/sudoers.d/recipehelper-deploy

# Restrict the deploy user's key to that one forced command. Paste the
# contents of ci_deploy_key.pub in place of the placeholder below.
mkdir -p /home/deploy/.ssh
cat <<'KEY' > /home/deploy/.ssh/authorized_keys
command="sudo /usr/local/bin/deploy-recipehelper.sh",no-port-forwarding,no-agent-forwarding,no-X11-forwarding,no-pty ssh-ed25519 AAAA...paste-ci_deploy_key.pub-contents-here... github-actions-deploy
KEY
chown -R deploy:deploy /home/deploy/.ssh
chmod 700 /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys
```

`www-data` already owns `/var/www/recipehelper` from the existing setup —
`deploy` never touches those files directly, only the root-run script does.

## 3. Add the private key to GitHub

Repo → **Settings → Environments → New environment** → name it `production`
(optionally check "Required reviewers" here so a deploy needs a manual
approval click even though it's triggered by a button) → **Add secret** →
name it `DEPLOY_SSH_KEY` → paste the full contents of `ci_deploy_key` (the
*private* key file from step 1).

Delete `ci_deploy_key` / `ci_deploy_key.pub` from your laptop once it's in
GitHub — you won't need the private key locally again.

## 4. Run it

Actions tab → **Deploy to Production** → **Run workflow** → optionally
type a branch name (e.g. a PR branch) instead of the `main` default →
**Run workflow**. Works the same from the GitHub mobile app.

Since this deploys directly to the live single-user app (no separate dev
instance), remember: whatever branch you deploy is what's live until the
next deploy. If you deploy a PR branch to check it and decide not to merge,
re-run the workflow with `main` to put the real branch back.

## Rotating or revoking access

To cut off CI entirely: delete `/home/deploy/.ssh/authorized_keys` (or the
whole `deploy` user) on the VM. Nothing else needs to change — the root
key and the manual `deploy/deploy.sh` path are unaffected.
