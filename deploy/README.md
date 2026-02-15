# RecipeHelper Deployment Guide

## Overview

RecipeHelper is deployed on an Oracle Cloud free-tier Ubuntu 22.04 VM with:

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
| VM IP | `170.9.247.161` |
| URL | `https://sutherlinsrecipes.duckdns.org` |
| SSH | `ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161` |
| App directory | `/var/www/recipehelper/` |
| Service name | `recipehelper` |
| nginx config | `/etc/nginx/sites-available/recipehelper` |
| SSL certs | `/etc/letsencrypt/live/sutherlinsrecipes.duckdns.org/` |

---

## Deploying Updates

After making code changes, run the deploy script from your repo root in git bash:

```bash
./deploy/deploy.sh
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
scp -i ~/Downloads/lrecipehelper101.key -r ./publish/* ubuntu@170.9.247.161:/tmp/recipehelper/

# 3. SSH in and deploy
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161
sudo systemctl stop recipehelper
sudo cp -r /tmp/recipehelper/* /var/www/recipehelper/
sudo chown -R www-data:www-data /var/www/recipehelper
sudo systemctl start recipehelper
```

---

## Common Operations

### Check if the app is running
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161 "sudo systemctl status recipehelper"
```

### View live logs
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161 "sudo journalctl -u recipehelper -f"
```

### Restart the app
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161 "sudo systemctl restart recipehelper"
```

### Check nginx status
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161 "sudo systemctl status nginx"
```

### Check SSL certificate expiry
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161 "sudo certbot certificates"
```

### Manually renew SSL certificate (normally auto-renews)
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@170.9.247.161 "sudo certbot renew"
```

---

## Firewall & Network Rules

There are **two firewalls** that must both allow traffic:

### 1. Oracle Cloud Security List (VCN level)

Configured in: **Oracle Cloud Console > Networking > VCN > Subnet > Security List**

| Direction | Source | Protocol | Port | Purpose |
|-----------|--------|----------|------|---------|
| Ingress | `0.0.0.0/0` | TCP | 80 | HTTP (redirects to HTTPS, also needed for cert renewal) |
| Ingress | `<YOUR-IP>/32` | TCP | 443 | HTTPS (restricted to your home IP) |
| Ingress | `0.0.0.0/0` | TCP | 22 | SSH |

### 2. VM iptables (OS level)

Already configured and persisted. Current rules:
```
1  ACCEPT  all   -- state RELATED,ESTABLISHED
2  ACCEPT  icmp  --
3  ACCEPT  all   -- (loopback)
4  ACCEPT  tcp   -- dpt:22  (SSH)
5  ACCEPT  tcp   -- dpt:80  (HTTP)
6  ACCEPT  tcp   -- dpt:443 (HTTPS)  ← must be BEFORE the REJECT rule
7  REJECT  all   -- (catch-all reject)
```

To view: `sudo iptables -L INPUT -n --line-numbers`

**Important:** When adding iptables rules, always insert them BEFORE the REJECT rule. The REJECT rule drops all traffic that hasn't matched a previous ACCEPT rule. If you add a new rule after it, traffic will be rejected before reaching your rule.

### If your home IP changes

Your ISP may occasionally change your public IP. If you can't access the site:

1. Find your new IP: visit https://api.ipify.org in your browser
2. Update the OCI Security List: change the port 443 source CIDR to `<new-ip>/32`

---

## Azure SQL Database

The app connects to Azure SQL Database remotely. The VM's IP (`170.9.247.161`) must be whitelisted in Azure:

**Azure Portal > SQL Server (sql-shopping-generator) > Networking > Firewall rules**

If the VM's IP ever changes, you'll need to update this rule too.
