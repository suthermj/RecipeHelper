#!/bin/bash
# One-time server setup for RecipeHelper on Ubuntu 22.04 (Oracle Cloud)
# Installs: .NET 8 runtime, nginx, certbot, systemd service
# After running this, use deploy.sh for subsequent deploys
set -e

DOMAIN="sutherlinsrecipes.duckdns.org"

echo "=== RecipeHelper Server Setup ==="
echo "Ubuntu 22.04 + .NET 8 + nginx + HTTPS"
echo ""

# --- 1. Install .NET 8 ASP.NET Runtime ---
echo "[1/6] Installing .NET 8 ASP.NET Runtime..."
wget -q https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update -qq
sudo apt-get install -y -qq aspnetcore-runtime-8.0
echo "  Done"

# --- 2. Install nginx ---
echo "[2/6] Installing nginx..."
sudo apt-get install -y -qq nginx
echo "  Done"

# --- 3. Create app directory ---
echo "[3/6] Setting up app directory..."
sudo mkdir -p /var/www/recipehelper
sudo chown www-data:www-data /var/www/recipehelper
echo "  /var/www/recipehelper created"

# --- 4. Create systemd service ---
echo "[4/6] Creating systemd service..."
sudo tee /etc/systemd/system/recipehelper.service > /dev/null <<'EOF'
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
EOF
sudo systemctl daemon-reload
sudo systemctl enable recipehelper
echo "  recipehelper.service created and enabled"

# --- 5. Configure nginx ---
echo "[5/6] Configuring nginx..."
sudo tee /etc/nginx/sites-available/recipehelper > /dev/null <<EOF
server {
    listen 80;
    server_name $DOMAIN;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF
sudo ln -sf /etc/nginx/sites-available/recipehelper /etc/nginx/sites-enabled/recipehelper
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl restart nginx
echo "  Done"

# --- 6. Install Certbot and get SSL certificate ---
echo "[6/6] Setting up HTTPS with Let's Encrypt..."
sudo apt-get install -y -qq certbot python3-certbot-nginx
sudo certbot --nginx -d "$DOMAIN" \
  --non-interactive --agree-tos --register-unsafely-without-email --redirect
echo "  HTTPS configured (auto-renews via certbot timer)"

# --- Open firewall ports ---
echo ""
echo "Opening firewall ports 80 and 443..."
# Insert BEFORE the REJECT rule (position 5 in default Oracle Cloud iptables)
sudo iptables -I INPUT 5 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 5 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo apt-get install -y -qq iptables-persistent
sudo netfilter-persistent save
echo "  Done"

echo ""
echo "=== Setup complete! ==="
echo ""
echo "Next steps:"
echo "  1. Deploy the app:  ./deploy.sh  (from your local machine)"
echo "  2. Oracle Cloud Console - add these ingress rules to your VCN Security List:"
echo "     - Port 80:  Source 0.0.0.0/0, TCP, Dest Port 80"
echo "     - Port 443: Source <YOUR-IP>/32, TCP, Dest Port 443"
echo "  3. Azure SQL - add the VM's public IP to the firewall allow list"
echo ""
echo "View logs:  sudo journalctl -u recipehelper -f"
echo "Site URL:   https://$DOMAIN"
