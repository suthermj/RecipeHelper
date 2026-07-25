#!/bin/bash
# Installed on the VM at /usr/local/bin/deploy-recipehelper.sh (root-owned,
# not writable by the `deploy` user — see README.md in this directory).
#
# This is the *only* thing the restricted `deploy` SSH user can ever run,
# via a forced command in authorized_keys. It reads a tar.gz of a
# `dotnet publish` output from stdin, replaces the live app, and restarts
# the systemd service. Mirrors the local deploy.sh flow used from a laptop.
set -euo pipefail

APP_DIR="/var/www/recipehelper"
CONFIG_DIR="/etc/recipehelper"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

tar -xzf - -C "$TMP_DIR"

# Data protection keys must persist across deploys or antiforgery tokens
# break on every restart — see CLAUDE.md "Known issue" note.
mkdir -p /var/lib/recipehelper/keys
chown -R www-data:www-data /var/lib/recipehelper/keys

systemctl stop recipehelper
rm -rf "${APP_DIR:?}"/*
cp -r "$TMP_DIR"/. "$APP_DIR"/

# Production config lives outside the app dir so it survives every deploy
# without needing to be re-uploaded. CI doesn't have the secrets file, so
# we copy it back from /etc/recipehelper/ after replacing the app files.
if [ -f "$CONFIG_DIR/appsettings.Production.json" ]; then
    cp "$CONFIG_DIR/appsettings.Production.json" "$APP_DIR/appsettings.Production.json"
else
    echo "WARNING: $CONFIG_DIR/appsettings.Production.json not found — app may fail to start" >&2
fi

chown -R www-data:www-data "$APP_DIR"
systemctl start recipehelper

sleep 3
systemctl status recipehelper --no-pager
curl -s -o /dev/null -w "HTTP %{http_code} from localhost:5000\n" http://localhost:5000
curl -s -o /dev/null -w "HTTP %{http_code} from https://sutherlinsrecipes.duckdns.org\n" https://sutherlinsrecipes.duckdns.org
