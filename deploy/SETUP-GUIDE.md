# Server Setup Guide — From Scratch

Step-by-step walkthrough of how RecipeHelper was deployed to Oracle Cloud. Use this to understand the process or to recreate it on a new VM.

---

## Prerequisites

- An Oracle Cloud account (free tier works)
- An Ubuntu 22.04 VM instance with a public IP
- An SSH key pair (Oracle generates one when you create the instance)
- Your app's Azure SQL Database connection string

---

## Step 1: Create the Oracle Cloud VM

1. Log into Oracle Cloud Console
2. **Compute > Instances > Create Instance**
3. Choose **Ubuntu 22.04** as the image, **AMD** shape (free tier eligible)
4. Download the SSH private key when prompted (e.g., `lrecipehelper101.key`)
5. Note the **public IP** once the instance is running

Test SSH access:
```bash
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@<VM-IP>
```

---

## Step 2: Install .NET 8 ASP.NET Runtime

The app is built with .NET 8. We only need the **runtime** on the server (not the SDK) because we publish from our Windows dev machine.

```bash
# Add Microsoft's package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install the ASP.NET Core runtime
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0
```

**Why just the runtime?** The SDK includes compilers and build tools (~700MB) that aren't needed on the server. The runtime (~100MB) is all you need to run a pre-compiled app. We compile on our dev machine with `dotnet publish` and just copy the output.

---

## Step 3: Publish the App (on your Windows machine)

```bash
cd RecipeHelper

# Build the Tailwind CSS first — this compiles utility classes into output.css
npm run css:build

# Publish for Linux x64 — framework-dependent (uses the runtime we installed)
dotnet publish -c Release -r linux-x64 --self-contained false -o ../publish
```

**Key flags:**
- `-c Release` — optimized build, no debug symbols
- `-r linux-x64` — target the Linux VM (not Windows)
- `--self-contained false` — use the runtime installed on the server (smaller output)
- The output in `./publish/` contains the DLL, config files, wwwroot static assets, and dependency DLLs

---

## Step 4: Upload to the VM

```bash
# Create a temp directory on the VM
ssh -i ~/Downloads/lrecipehelper101.key ubuntu@<VM-IP> "mkdir -p /tmp/recipehelper"

# Copy the published files
scp -i ~/Downloads/lrecipehelper101.key -r ./publish/* ubuntu@<VM-IP>:/tmp/recipehelper/
```

Then on the VM, move them to the app directory:
```bash
sudo mkdir -p /var/www/recipehelper
sudo cp -r /tmp/recipehelper/* /var/www/recipehelper/
sudo chown -R www-data:www-data /var/www/recipehelper
```

**Why `/var/www/recipehelper`?** This is the conventional Linux location for web apps. The `www-data` user is nginx's default user — the app runs as this user for security (minimal permissions).

---

## Step 5: Create a systemd Service

systemd is Linux's service manager. It will:
- Start the app automatically when the VM boots
- Restart it if it crashes
- Provide logging via `journalctl`

Create the service file:
```bash
sudo nano /etc/systemd/system/recipehelper.service
```

Contents:
```ini
[Unit]
Description=RecipeHelper .NET App
After=network.target

[Service]
WorkingDirectory=/var/www/recipehelper
ExecStart=/usr/bin/dotnet /var/www/recipehelper/RecipeHelper.dll
Restart=always
RestartSec=10
SyslogIdentifier=recipehelper
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

**What each section does:**
- `After=network.target` — wait for networking before starting
- `ExecStart` — the command to run your app
- `Restart=always` — if the process exits for any reason, restart it after 10 seconds
- `User=www-data` — run as the unprivileged web user, not root
- `ASPNETCORE_URLS=http://localhost:5000` — only listen on localhost (nginx handles external traffic)
- `ASPNETCORE_ENVIRONMENT=Production` — tells .NET to use production config and error handling

Enable and start:
```bash
sudo systemctl daemon-reload    # reload service definitions
sudo systemctl enable recipehelper  # start on boot
sudo systemctl start recipehelper   # start now
sudo systemctl status recipehelper  # verify it's running
```

---

## Step 6: Install and Configure nginx

nginx acts as a **reverse proxy** — it accepts external HTTP/HTTPS requests and forwards them to your .NET app on localhost:5000.

**Why not expose Kestrel directly?**
- nginx handles SSL termination (HTTPS)
- nginx serves static files more efficiently
- nginx provides buffering, connection management, and security hardening
- Kestrel (the .NET web server) is designed to sit behind a reverse proxy in production

```bash
sudo apt-get install -y nginx
```

Create the site config:
```bash
sudo nano /etc/nginx/sites-available/recipehelper
```

Contents:
```nginx
server {
    listen 80;
    server_name sutherlinsrecipes.duckdns.org;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

**What the proxy headers do:**
- `Host` — passes the original domain name to your app
- `X-Forwarded-For` — passes the client's real IP (otherwise your app only sees 127.0.0.1)
- `X-Forwarded-Proto` — tells your app whether the original request was HTTP or HTTPS

Enable the site:
```bash
# Symlink to sites-enabled (this is how nginx knows which configs to load)
sudo ln -sf /etc/nginx/sites-available/recipehelper /etc/nginx/sites-enabled/recipehelper

# Remove the default "Welcome to nginx" page
sudo rm -f /etc/nginx/sites-enabled/default

# Test the config for syntax errors
sudo nginx -t

# Apply the changes
sudo systemctl restart nginx
```

---

## Step 7: Open Firewall Ports

Oracle Cloud VMs have **two firewalls** — you must open ports in both.

### VM-level firewall (iptables)

Oracle's Ubuntu images ship with iptables rules that block everything except SSH. The rules are ordered — traffic is tested against each rule top-to-bottom, and the first match wins.

The default rules look like:
```
1  ACCEPT  state RELATED,ESTABLISHED   ← allow responses to outgoing connections
2  ACCEPT  icmp                        ← allow ping
3  ACCEPT  all (loopback)              ← allow localhost traffic
4  ACCEPT  tcp dpt:22                  ← allow SSH
5  REJECT  all                         ← block everything else
```

We need to insert HTTP (80) and HTTPS (443) rules **before** the REJECT rule at position 5:

```bash
# Insert at position 5 (pushes REJECT to position 7)
sudo iptables -I INPUT 5 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 5 -m state --state NEW -p tcp --dport 443 -j ACCEPT

# Persist the rules across reboots
sudo apt-get install -y iptables-persistent
sudo netfilter-persistent save
```

**Common gotcha:** If you append rules instead of inserting them before REJECT, they'll never match because the REJECT rule catches traffic first. Always use `-I INPUT <position>` to insert at a specific position.

### Oracle Cloud Security List (VCN level)

This is a cloud-level firewall configured in the Oracle Cloud Console:

1. Go to **Networking > Virtual Cloud Networks**
2. Click your VCN > click your **Subnet** > click the **Security List**
3. Add **Ingress Rules**:

| Source CIDR | Protocol | Dest Port | Purpose |
|-------------|----------|-----------|---------|
| `0.0.0.0/0` | TCP | 80 | HTTP (cert renewal + redirect) |
| `<your-ip>/32` | TCP | 443 | HTTPS (your access only) |

Port 80 is open to all because Let's Encrypt needs to reach it for certificate renewal. HTTP requests are automatically redirected to HTTPS by nginx anyway.

Port 443 is restricted to your home IP for security. Find your IP at https://api.ipify.org.

---

## Step 8: Set Up HTTPS with Let's Encrypt

### Get a free domain with DuckDNS

Let's Encrypt won't issue certificates for bare IP addresses. DuckDNS provides free subdomains:

1. Go to https://www.duckdns.org and sign in
2. Create a subdomain (e.g., `sutherlinsrecipes`)
3. Set the IP to your VM's public IP (`170.9.247.161`)

### Install Certbot

Certbot is the official Let's Encrypt client. The nginx plugin automatically configures SSL in your nginx config.

```bash
sudo apt-get install -y certbot python3-certbot-nginx
```

### Get the certificate

```bash
sudo certbot --nginx -d sutherlinsrecipes.duckdns.org \
  --non-interactive --agree-tos --register-unsafely-without-email --redirect
```

**What this does:**
1. Contacts Let's Encrypt and proves you control the domain (via an HTTP challenge on port 80)
2. Downloads the SSL certificate and private key to `/etc/letsencrypt/live/sutherlinsrecipes.duckdns.org/`
3. Automatically modifies your nginx config to:
   - Listen on port 443 with SSL
   - Redirect all HTTP (port 80) traffic to HTTPS
4. Sets up a systemd timer to auto-renew before expiry (certificates last 90 days)

The `--redirect` flag adds an automatic HTTP-to-HTTPS redirect, so anyone hitting `http://` gets sent to `https://`.

### Verify auto-renewal

```bash
# Check the renewal timer is active
sudo systemctl status certbot.timer

# Dry-run renewal to test it works
sudo certbot renew --dry-run
```

---

## Step 9: Azure SQL Firewall

Since the app connects to Azure SQL Database remotely, the VM's IP must be allowed:

1. **Azure Portal** > your SQL Server (`sql-shopping-generator`)
2. **Networking** > **Public access** tab
3. Under **Firewall rules**, add:
   - Name: `OracleCloudVM`
   - Start IP: `170.9.247.161`
   - End IP: `170.9.247.161`
4. Save

---

## Troubleshooting

### App returns 500 error
Check the logs — usually a database connection or config issue:
```bash
sudo journalctl -u recipehelper -n 50 --no-pager
```

### Can't reach the site from your browser
Work through the layers:
```bash
# 1. Is the app running?
sudo systemctl status recipehelper

# 2. Does it respond locally?
curl http://localhost:5000

# 3. Does nginx respond locally?
curl http://localhost

# 4. Are iptables rules correct?
sudo iptables -L INPUT -n --line-numbers

# 5. Check Oracle Cloud Security List in the console
```

### SSL certificate expired
Should auto-renew, but if it didn't:
```bash
sudo certbot renew
sudo systemctl reload nginx
```

### Home IP changed (can't access HTTPS)
1. Find new IP: https://api.ipify.org
2. Update Oracle Cloud Security List: change port 443 source to `<new-ip>/32`
