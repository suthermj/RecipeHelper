#!/bin/bash
# Deploys RecipeHelper to the Hetzner VM
# Run from the RecipeHelper repo root on your local Windows machine (git bash)
set -e

VM_USER="root"
VM_HOST="178.105.73.57"
SSH_KEY="~/.ssh/hetzner"
REMOTE_APP_DIR="/var/www/recipehelper"

echo "=== RecipeHelper Deploy ==="

# 1. Build CSS and publish for Linux
echo "[1/4] Building CSS..."
cd RecipeHelper
npm run css:build

echo "[2/4] Publishing for Linux x64..."
dotnet publish -c Release -r linux-x64 --self-contained false -o ../publish
cd ..

# 2. Upload to VM
echo "[3/4] Uploading to VM..."
rsync -az --delete -e "ssh -i $SSH_KEY" ./publish/ "$VM_USER@$VM_HOST:/tmp/recipehelper/"

# 3. Deploy on VM: stop service, copy files, restart
echo "[4/4] Deploying on VM..."
ssh -i "$SSH_KEY" "$VM_USER@$VM_HOST" bash -s <<'REMOTE'
set -e
systemctl stop recipehelper
cp -r /tmp/recipehelper/* /var/www/recipehelper/
chown -R www-data:www-data /var/www/recipehelper
systemctl start recipehelper
sleep 3
echo ""
sudo systemctl status recipehelper --no-pager
echo ""
echo "Health check:"
curl -s -o /dev/null -w "  HTTP %{http_code} from localhost:5000\n" http://localhost:5000
curl -s -o /dev/null -w "  HTTP %{http_code} from https://sutherlinsrecipes.duckdns.org\n" https://sutherlinsrecipes.duckdns.org
rm -rf /tmp/recipehelper/*
REMOTE

echo ""
echo "=== Deploy complete! ==="
echo "https://sutherlinsrecipes.duckdns.org"
