#!/bin/bash

DOMAIN="example.com"
EMAIL="example@example.com"

issue_wildcard() {
  docker-compose -f /home/ubuntu/Maroik/docker-compose.prod.yml run --rm certbot certonly \
    --dns-cloudflare \
    --dns-cloudflare-credentials /etc/letsencrypt/cloudflare.ini \
    --config-dir /etc/letsencrypt \
    --work-dir /etc/letsencrypt \
    --logs-dir /var/log/letsencrypt \
    -d $DOMAIN -d "*.$DOMAIN" \
    --non-interactive \
    --agree-tos \
    -m $EMAIL
}

issue_wildcard_force() {
  docker-compose -f /home/ubuntu/Maroik/docker-compose.prod.yml run --rm certbot certonly \
    --dns-cloudflare \
    --dns-cloudflare-credentials /etc/letsencrypt/cloudflare.ini \
    --config-dir /etc/letsencrypt \
    --work-dir /etc/letsencrypt \
    --logs-dir /var/log/letsencrypt \
    -d $DOMAIN -d "*.$DOMAIN" \
    --non-interactive \
    --agree-tos \
    -m $EMAIL \
    --force-renewal
}

renew() {
  docker-compose -f /home/ubuntu/Maroik/docker-compose.prod.yml run --rm certbot renew \
    --dns-cloudflare --dns-cloudflare-credentials /etc/letsencrypt/cloudflare.ini \
    --deploy-hook "
      echo 'Renewed PEM files'
    "
}

renew_force() {
  docker-compose -f /home/ubuntu/Maroik/docker-compose.prod.yml run --rm certbot renew \
    --force-renewal \
    --dns-cloudflare --dns-cloudflare-credentials /etc/letsencrypt/cloudflare.ini \
    --deploy-hook "
      echo 'Renewed PEM files'
    "
}

# Usage example:
# ./CertbotScript.sh issue_wildcard       (for issuing new certificate)
# ./CertbotScript.sh issue_wildcard_force (for forced issuance)
# ./CertbotScript.sh renew                (for renewing certificate)
# ./CertbotScript.sh renew_force          (for forced renewal)

case "$1" in
  issue_wildcard)
    issue_wildcard
    ;;
  issue_wildcard_force)
    issue_wildcard_force
    ;;
  renew)
    renew
    ;;
  renew_force)
    renew_force
    ;;
  *)
    echo "Usage: $0 {issue_wildcard|issue_wildcard_force|renew|renew_force}"
    exit 1
    ;;
esac
