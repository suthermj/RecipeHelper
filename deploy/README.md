# RecipeHelper Deployment Guide

## Overview

RecipeHelper is deployed on a Hetzner VPS (Ubuntu 22.04) with:

- **.NET 8 ASP.NET Runtime** — runs the app
- **nginx** — reverse proxy, handles HTTPS termination
- **systemd** — keeps the app running and auto-starts on reboot
- **Let's Encrypt (Certbot)** — free SSL certificate, auto-renews
- **DuckDNS** — free subdomain pointing to the VM

```
Browser → https://sutherlinsrecipes.duckdns.org
         → nginx (port 443, SSL termination)
           → Kestrel (localhost:5000, HTTP)
             → Azure SQL Database
```

## Server Details

| Item | Value |
|------|-------|
| Provider | Hetzner VPS |
| VM IP | `178.105.73.57` |
| URL | `https://sutherlinsrecipes.duckdns.org` |
| SSH | `ssh -i ~/.ssh/hetzner root@178.105.73.57` |
| App directory | `/var/www/recipehelper/` |
| Service name | `recipehelper` |
| nginx config | `/etc/nginx/sites-available/recipehelper` |
| SSL certs | `/etc/letsencrypt/live/sutherlinsrecipes.duckdns.org/` |

---

## Deploying Updates

After making code changes, run the deploy script from your repo root in git bash:

```bash
bash deploy/deploy.sh
```

This script does everything in one shot:
1. Builds Tailwind CSS (`npm run css:build`)
2. Publishes the app for Linux x64 (`dotnet publish`)
3. Uploads the files to the VM via SCP
4. Stops the service, copies files, restarts the service
5. Runs a health check

### Manual deploy (if you prefer step by step)

```bash
# 1. Build and publish locally
cd RecipeHelper
npm run css:build
dotnet publish -c Release -r linux-x64 --self-contained false -o ../publish
cd ..

# 2. Upload to VM
scp -i ~/.ssh/hetzner -r ./publish/* root@178.105.73.57:/tmp/recipehelper/

# 3. SSH in and deploy
ssh -i ~/.ssh/hetzner root@178.105.73.57
systemctl stop recipehelper
cp -r /tmp/recipehelper/* /var/www/recipehelper/
chown -R www-data:www-data /var/www/recipehelper
systemctl start recipehelper
```

---

## Common Operations

### Check if the app is running
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "systemctl status recipehelper"
```

### View live logs
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "journalctl -u recipehelper -f"
```

### Restart the app
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "systemctl restart recipehelper"
```

### Check nginx status
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "systemctl status nginx"
```

### Check SSL certificate expiry
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "certbot certificates"
```

### Manually renew SSL certificate (normally auto-renews)
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "certbot renew"
```

---

## Firewall & Network

Hetzner VPS uses standard `ufw` or `iptables` at the OS level. There is no cloud-level security list (unlike Oracle Cloud).

To check current iptables rules:
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "iptables -L INPUT -n --line-numbers"
```

Required open ports: 22 (SSH), 80 (HTTP/cert renewal), 443 (HTTPS).

---

## Azure SQL Database

The app connects to Azure SQL Database remotely. The VM's IP (`178.105.73.57`) must be whitelisted in Azure:

**Azure Portal > SQL Server (sql-shopping-generator) > Networking > Firewall rules**

If the VM's IP ever changes, update this rule.

---

## Troubleshooting

### App returns 500 error
Check the logs — usually a database connection or config issue:
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "journalctl -u recipehelper -n 50 --no-pager"
```

### Can't reach the site from your browser
Work through the layers:
```bash
# 1. Is the app running?
ssh -i ~/.ssh/hetzner root@178.105.73.57 "systemctl status recipehelper"

# 2. Does it respond locally?
ssh -i ~/.ssh/hetzner root@178.105.73.57 "curl http://localhost:5000"

# 3. Does nginx respond?
ssh -i ~/.ssh/hetzner root@178.105.73.57 "systemctl status nginx"
```

### SSL certificate expired
Should auto-renew, but if it didn't:
```bash
ssh -i ~/.ssh/hetzner root@178.105.73.57 "certbot renew && systemctl reload nginx"
```
