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

Two layers:

1. **Hetzner Cloud Firewall** (cloud console, applied at the VM's network interface). SSH (port 22) is restricted by source IP — if your home IP rotates, `deploy.sh` will hang on the `scp` step with a connection timeout. Fix:
   - Get current IP: `curl -s https://api.ipify.org`
   - Hetzner Cloud Console → Firewalls → edit the SSH inbound rule → add/replace the IP
   - Public HTTP/HTTPS (80/443) should remain open to `0.0.0.0/0` for site access and Let's Encrypt renewals.

2. **OS-level iptables** on the VM:
   ```bash
   ssh -i ~/.ssh/hetzner root@178.105.73.57 "iptables -L INPUT -n --line-numbers"
   ```

Required open ports overall: 22 (SSH), 80 (HTTP/cert renewal), 443 (HTTPS).

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

---

## Observability (Grafana Cloud)

The app sends OpenTelemetry data (traces, metrics, logs) to Grafana Cloud over OTLP/HTTP.

**Config lives in `RecipeHelper/appsettings.Production.json`** — that file is gitignored but the `dotnet publish` step in `deploy.sh` bundles it into the publish output, so deploying ships the config automatically. The relevant section:

```json
"OpenTelemetry": {
  "ServiceName": "recipe-helper",
  "ServiceNamespace": "recipe-helper",
  "Otlp": {
    "Endpoint": "https://otlp-gateway-prod-us-east-3.grafana.net/otlp",
    "Protocol": "http/protobuf",
    "Headers": "Authorization=Basic <base64(instanceId:glc_token)>"
  }
}
```

`Program.cs` wires three exporters and appends `/v1/traces`, `/v1/metrics`, `/v1/logs` to the base endpoint per signal (the .NET SDK does not auto-append when `Endpoint` is set programmatically).

In Grafana Cloud, filter by `service_name="recipe-helper"`. Traces appear immediately; metrics export on a 60-second interval.

**To add VM host metrics (CPU, memory, disk, network):** Grafana Cloud → Connections → Integrations → "Linux Server". It generates a one-line installer for Grafana Alloy plus pre-built dashboards. Not covered by the app's OpenTelemetry instrumentation.
