#!/bin/bash

CF_IPV4_URL="https://www.cloudflare.com/ips-v4"
CF_IPV6_URL="https://www.cloudflare.com/ips-v6"

echo "[+] Removing old Cloudflare rules..."
sudo ufw --force reset
sudo ufw default deny incoming
sudo ufw default allow outgoing

echo "[+] Adding Cloudflare IPs..."
for ip in $(curl -s $CF_IPV4_URL) $(curl -s $CF_IPV6_URL); do
    sudo ufw allow from $ip to any port 80 proto tcp
    sudo ufw allow from $ip to any port 443 proto tcp
    #sudo ufw allow from $ip to any port 5001 proto tcp
    #sudo ufw allow from $ip to any port 8080 proto tcp
done

sudo ufw --force enable

echo "[+] Cloudflare UFW rules updated."
