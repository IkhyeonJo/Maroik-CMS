#!/bin/bash

# Full deploy of Maroik. Run it as ubuntu (bash Maroik.DeployOps/Deploy.sh); it asks how to deploy:
#   1) Build here — builds the three app images (Website, FileStorage, Worker) from the source in /home/ubuntu/Maroik as it is
#      (a git clone on any branch or commit, or source copied here by hand) and starts the stack. Nothing is fetched, checked out
#      or pulled from GitHub / ghcr.io, and AutoDeploy.sh is not installed (it follows GitHub's main and would replace this build).
#   2) GitHub — checks out the newest main whose CI/CD publish job succeeded (.github/workflows/ci-cd.yml), pulls its three images
#      (ghcr.io/ikhyeonjo/maroik-*:sha-<12-char commit>) and installs AutoDeploy.sh, which keeps production on main from then on.
#      SKIP_CI_CHECK=1 skips the CI lookup (e.g. GitHub's API is down); the images must still exist.
#   3) Quit.
# Without a terminal (or to skip the question) pass the choice: bash Maroik.DeployOps/Deploy.sh local | github.
# Choice 1 then asks how visitors reach the site, and starts the stack for it (docker-compose.prod.<mode>.yml over
# docker-compose.prod.yml; the website reads it as ServerSetting:HostingMode). Nothing is saved: it is asked on every deploy.
#   no domain          -> plain HTTP on a port of this server, for a home network (no HTTPS)
#   domain + tunnel    -> Cloudflare Tunnel (cloudflared, TUNNEL_TOKEN in .env.prod), no port opened
#   domain + port fwd  -> Caddy on 80 / 443 with a Let's Encrypt certificate; the router forwards 80 / 443 here
# Without a terminal pass the answers too:
#   bash Maroik.DeployOps/Deploy.sh local nodomain [port] [mail-link address]
#   bash Maroik.DeployOps/Deploy.sh local tunnel <domain>
#   bash Maroik.DeployOps/Deploy.sh local portforward <domain> <Let's Encrypt e-mail>
# Choice 2 always runs the Cloudflare Tunnel with .env.prod's domain, as AutoDeploy.sh does.
# Either way the server-only files (.env.prod, whose template is .env.prod.example, the Production Init.sql and the upload folders)
# are checked first; they are git-ignored, so a checkout never touches them.
REPO_DIR="/home/ubuntu/Maroik"
REPO_URL="https://github.com/IkhyeonJo/Maroik.git"
APP_SERVICES="maroik.website maroik.filestorage maroik.worker"

# Run as ubuntu, not with sudo: the clone must belong to ubuntu, because AutoDeploy.sh (root's cron) runs git as
# ubuntu. The commands below that need root use sudo themselves.
if [ "$(id -u)" = "0" ]; then
  echo "[ERROR] Run Deploy.sh as ubuntu (bash Maroik.DeployOps/Deploy.sh), not as root or with sudo."
  exit 1
fi
cd "$REPO_DIR" || exit 1

# ── How to deploy ───────────────────────────────────────────────────────────────────────────────────────────────────
MODE="${1:-}"
if [ -z "$MODE" ]; then
  if [ ! -t 0 ]; then
    echo "[ERROR] No terminal to ask how to deploy: pass it (bash Maroik.DeployOps/Deploy.sh local | github)."
    exit 1
  fi
  echo "How to deploy Maroik?"
  echo "  1) Build here from the source in $REPO_DIR (no GitHub)"
  echo "  2) GitHub: the newest main with the images CI published, kept current by AutoDeploy.sh"
  echo "  3) Quit"
  read -r -p "Choose [1-3]: " MODE
fi
case "$MODE" in
  1|local) MODE="local" ;;
  2|github) MODE="github" ;;
  3|quit|q) echo "[INFO] Not deploying."; exit 0 ;;
  *) echo "[ERROR] Unknown choice '$MODE': choose 1 (local), 2 (github) or 3 (quit)."; exit 1 ;;
esac

# ── How visitors reach the site (choice 1 only) ─────────────────────────────────────────────────────────────
ENV_PROD_PATH="$REPO_DIR/.env.prod"
env_value() { grep "^$1=" "$ENV_PROD_PATH" 2>/dev/null | head -1 | cut -d '=' -f2- | tr -d '\r'; }
# Asks $1 with the default $2 (taken on Enter); without a terminal the default is the answer.
ask() {
  local answer=""
  if [ -t 0 ]; then read -r -p "$1 [$2]: " answer; fi
  echo "${answer:-$2}"
}
# maroik.com from "https://www.maroik.com/", "www.maroik.com" or "maroik.com".
bare_domain() { echo "$1" | sed -E 's#^[a-zA-Z]+://##; s#/.*$##; s#^www\.##' | tr '[:upper:]' '[:lower:]'; }

HOSTING="tunnel"
MAROIK_DOMAIN=""
if [ "$MODE" = "local" ]; then
  HOSTING="${2:-}"
  if [ -z "$HOSTING" ]; then
    if [ ! -t 0 ]; then
      echo "[ERROR] No terminal to ask how the site is reached: pass it (bash Maroik.DeployOps/Deploy.sh local nodomain | tunnel <domain> | portforward <domain> <email>)."
      exit 1
    fi
    read -r -p "Does the site have a domain? [y/N] " has_domain
    if [ "$has_domain" = "y" ] || [ "$has_domain" = "Y" ]; then
      echo "How is it reached?"
      echo "  1) Cloudflare Tunnel (cloudflared; no port is opened)"
      echo "  2) Port forwarding (the router forwards 80 / 443 to this server; Let's Encrypt certificate)"
      read -r -p "Choose [1-2]: " reach
      case "$reach" in
        1) HOSTING="tunnel" ;;
        2) HOSTING="portforward" ;;
        *) echo "[ERROR] Unknown choice '$reach': choose 1 (Cloudflare Tunnel) or 2 (port forwarding)."; exit 1 ;;
      esac
    else
      HOSTING="nodomain"
    fi
  fi
  case "$HOSTING" in
    tunnel|portforward)
      MAROIK_DOMAIN=$(bare_domain "${3:-$(ask "Domain (www.<domain> is served too)" "$(bare_domain "$(env_value ServerSetting__DomainName)")")}")
      if ! echo "$MAROIK_DOMAIN" | grep -Eq '^([a-z0-9]([a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$'; then
        echo "[ERROR] '$MAROIK_DOMAIN' is not a domain (e.g. maroik.com)."; exit 1
      fi
      if [ "$HOSTING" = "portforward" ]; then
        MAROIK_ACME_EMAIL="${4:-$(ask "E-mail for Let's Encrypt (certificate expiry notices)" "$(env_value ServerSetting__SmtpOptions__fromEmail)")}"
        case "$MAROIK_ACME_EMAIL" in
          ?*@?*.?*) ;;
          *) echo "[ERROR] '$MAROIK_ACME_EMAIL' is not an e-mail address."; exit 1 ;;
        esac
      fi
      ;;
    nodomain)
      MAROIK_HTTP_PORT="${3:-$(ask "Port to serve the site on" "80")}"
      if ! echo "$MAROIK_HTTP_PORT" | grep -Eq '^[0-9]+$' || [ "$MAROIK_HTTP_PORT" -lt 1 ] || [ "$MAROIK_HTTP_PORT" -gt 65535 ]; then
        echo "[ERROR] '$MAROIK_HTTP_PORT' is not a port (1-65535)."; exit 1
      fi
      LAN_IP=$(hostname -I 2>/dev/null | awk '{print $1}')
      if [ "$MAROIK_HTTP_PORT" = "80" ]; then default_url="http://${LAN_IP:-localhost}/"; else default_url="http://${LAN_IP:-localhost}:$MAROIK_HTTP_PORT/"; fi
      MAROIK_SITE_URL="${4:-$(ask "Address the mails link to" "$default_url")}"
      case "$MAROIK_SITE_URL" in
        http://?*) ;;
        *) echo "[ERROR] '$MAROIK_SITE_URL' is not an http:// address."; exit 1 ;;
      esac
      echo "[WARN] Without a domain the site is plain HTTP: passwords and sessions travel unencrypted. Use it on a home network only."
      ;;
    *) echo "[ERROR] Unknown way to reach the site '$HOSTING': nodomain, tunnel or portforward."; exit 1 ;;
  esac
fi

# ── Preflight: the server-only files this repository does not carry ─────────────────────────────────────────────────
# Checked first, before git, apt or docker do anything, and reported all at once.
SQL_PROD_PATH="$REPO_DIR/Maroik.DB/PostgreSQL/SQL_Init_Script/Production/Init.sql"
UPLOAD_PATH="$REPO_DIR/Maroik.FileStorage/upload"
missing=()

if [ ! -f "$ENV_PROD_PATH" ]; then
  missing+=(".env.prod not found: copy .env.prod.example to $ENV_PROD_PATH and fill in the real values.")
else
  # The tunnel's token: only the Cloudflare Tunnel needs it.
  [ "$HOSTING" != "tunnel" ] || [ -n "$(env_value TUNNEL_TOKEN)" ] \
    || missing+=(".env.prod: TUNNEL_TOKEN is empty (Cloudflare Zero Trust > Networks > Tunnels > connector token).")
  case "$(env_value EXTERNAL_DRIVE_UUID)" in
    ""|"<CHANGE_ME>") missing+=(".env.prod: EXTERNAL_DRIVE_UUID is empty (the backup drive's UUID: sudo blkid).") ;;
  esac
  [ -n "$(env_value BACKUP_BASE_DIR)" ] \
    || missing+=(".env.prod: BACKUP_BASE_DIR is empty (the backup folder on the external drive).")
fi

if [ ! -s "$SQL_PROD_PATH" ]; then
  missing+=("Production Init.sql not found or empty: put the Init.sql of the latest Backup.sh archive at $SQL_PROD_PATH.")
fi

if [ ${#missing[@]} -gt 0 ]; then
  echo "[ERROR] Not ready to deploy:"
  for m in "${missing[@]}"; do echo "  - $m"; done
  exit 1
fi

# The uploaded files are not required (a new server has none), but forgetting to copy them back after Undeploy.sh is
# easy, so an empty folder needs a yes.
if [ -z "$(find "$UPLOAD_PATH" -type f ! -name README.md -print -quit 2>/dev/null)" ]; then
  echo "[WARN] $UPLOAD_PATH has no uploaded files (forum / calendar / note attachments)."
  echo "[WARN] Copy them from the latest Backup.sh archive first, unless this is a new server."
  if [ "${ALLOW_EMPTY_UPLOAD:-0}" != "1" ]; then
    if [ -t 0 ]; then
      read -r -p "Deploy without uploaded files? [y/N] " answer
      [ "$answer" = "y" ] || [ "$answer" = "Y" ] || { echo "[INFO] Not deploying."; exit 1; }
    else
      echo "[ERROR] Not deploying. Set ALLOW_EMPTY_UPLOAD=1 to deploy without uploaded files."
      exit 1
    fi
  fi
fi

# .env.prod holds the database password, the SMTP password and the tunnel token: owner-only.
[ -f "$REPO_DIR/.env.prod" ] && chmod 600 "$REPO_DIR/.env.prod"

if [ "$MODE" = "github" ]; then
  command -v git > /dev/null 2>&1 || sudo apt install -y git

  # First deploy on this box: only a backup (Backup.sh) or the server-only files were copied here. Turn the folder into
  # a clone of the public repository in place (git clone refuses a non-empty folder). None of the server-only files is
  # tracked there, so the checkout below adds the source next to them and never overwrites one.
  if [ ! -d "$REPO_DIR/.git" ]; then
    echo "[INFO] $REPO_DIR is not a git clone yet: fetching $REPO_URL into it."
    git init -q -b main "$REPO_DIR" \
      && git -C "$REPO_DIR" remote add origin "$REPO_URL" \
      || { echo "[ERROR] git init failed"; exit 1; }
  fi

  # Deploy.sh makes the tracked Maroik.Crontab/*.sh executable (chmod below), which git would otherwise report as a
  # local change; the next checkout of a commit that changes one of them would then refuse to run (AutoDeploy.sh).
  git -C "$REPO_DIR" config core.fileMode false

  # The deploy bookkeeping files live in the clone but are not part of the repository.
  for f in .deployed-sha .failed-sha; do
    grep -qxF "$f" "$REPO_DIR/.git/info/exclude" 2>/dev/null || echo "$f" >> "$REPO_DIR/.git/info/exclude"
  done

  git fetch -q origin main || { echo "[ERROR] git fetch failed"; exit 1; }
  DEPLOY_SHA=$(git rev-parse origin/main)

  if [ "${SKIP_CI_CHECK:-0}" != "1" ]; then
    CI_STATE=$(curl -fsS "https://api.github.com/repos/IkhyeonJo/Maroik/commits/$DEPLOY_SHA/check-runs" \
      | python3 -c "import sys,json; r=[c for c in json.load(sys.stdin)['check_runs'] if c['name']=='publish']; print((r[0]['conclusion'] or r[0]['status']) if r else 'none')")
    if [ "$CI_STATE" != "success" ]; then
      echo "[INFO] CI publish for $DEPLOY_SHA is '$CI_STATE', not success. Not deploying."
      echo "[INFO] Wait for the publish job of the CI workflow, or deploy an older commit by hand."
      exit 1
    fi
  fi

  # -f: the folder may hold copies of tracked files that git does not know yet (source copied by hand from an older
  # deploy, or docker-compose.prod.yml / Maroik.Crontab / Maroik.DeployOps extracted from a backup or from before they
  # were tracked); the repository's version replaces them. Git-ignored files (the server-only ones) are left alone.
  git checkout -f -q --detach "$DEPLOY_SHA" || { echo "[ERROR] git checkout failed"; exit 1; }
  MAROIK_TAG="sha-${DEPLOY_SHA:0:12}"
  echo "[INFO] Deploying $DEPLOY_SHA ($MAROIK_TAG)"
else
  # The image tag names what was built: the commit when this folder is a git clone (with "-dirty" when it has local
  # changes), otherwise the build time. Git is only read here, never asked to fetch.
  if git -C "$REPO_DIR" rev-parse --verify -q HEAD > /dev/null 2>&1; then
    SOURCE_REVISION=$(git -C "$REPO_DIR" rev-parse --short=12 HEAD)
    [ -n "$(git -C "$REPO_DIR" status --porcelain --untracked-files=no 2>/dev/null)" ] && SOURCE_REVISION="$SOURCE_REVISION-dirty"
    APP_VERSION=$(git -C "$REPO_DIR" describe --tags --abbrev=0 --match 'v[0-9]*.[0-9]*.[0-9]*' 2>/dev/null | sed 's/^v//')
  else
    SOURCE_REVISION="build-$(date '+%Y%m%d%H%M%S')"
  fi
  APP_VERSION="${APP_VERSION:-0.0.0}"
  MAROIK_TAG="local-$SOURCE_REVISION"
  echo "[INFO] Deploying the source in $REPO_DIR ($MAROIK_TAG)"
fi

# Target upload directories
paths=(
  "/home/ubuntu/Maroik/Maroik.Website/wwwroot/upload"
  "/home/ubuntu/Maroik/Maroik.FileStorage/upload"
)

for path in "${paths[@]}"; do
  # Remove executable permission from all files (set to 644: rw-r--r--)
  find "$path" -type f -exec chmod 644 {} \;

  # Set 755 permission on all directories (rwxr-xr-x)
  find "$path" -type d -exec chmod 755 {} \;
done

# Set Maroik Logs
mkdir -p /home/ubuntu/Maroik/Maroik.Log
chmod 755 /home/ubuntu/Maroik/Maroik.Log

# apt upgrade auto restart services
sudo sed -i 's/#$nrconf{restart} = '"'"'i'"'"';/$nrconf{restart} = '"'"'a'"'"';/g' /etc/needrestart/needrestart.conf

# Modify logind.conf to ignore lid switch (only once: every deploy reruns this script)
if ! grep -q '^HandleLidSwitch=ignore$' /etc/systemd/logind.conf; then
  echo "HandleLidSwitch=ignore" | sudo tee -a /etc/systemd/logind.conf
  sudo systemctl restart systemd-logind
fi

# Update and upgrade system
sudo apt update -y
sudo apt upgrade -y

# Remove what the upgrade replaced (old kernels, their modules and headers in / and /boot, orphaned
# dependencies) and the downloaded .deb files; apt keeps them all otherwise, a little more on every deploy.
sudo apt autoremove --purge -y
sudo apt clean

## Enable HTTPS nextcloud
#sudo nextcloud.enable-https self-signed

## Change nextcloud ports
#sudo snap set nextcloud ports.http=8079
#sudo snap set nextcloud ports.https=8080

# Install Docker Compose v2 (`docker compose`). v1 (the `docker-compose` package, 1.29) fails with
# KeyError 'ContainerConfig' when it recreates a container on recent Docker Engine versions, which is what
# AutoDeploy.sh does on every deploy. The plugin comes from the same place as the engine: docker-compose-plugin with
# Docker's own docker-ce, docker-compose-v2 with Ubuntu's docker.io (installing the Ubuntu one next to docker-ce would
# pull in docker.io and clash with it).
if ! sudo docker compose version > /dev/null 2>&1; then
  if dpkg -s docker-ce > /dev/null 2>&1; then
    sudo apt install -y docker-compose-plugin
  else
    sudo apt install -y docker-compose-v2
  fi
fi
if ! sudo docker compose version > /dev/null 2>&1; then
  echo "[ERROR] Docker Compose v2 (docker compose) is not available. Install it and run Deploy.sh again."
  exit 1
fi
COMPOSE="docker compose"

# Set permissions for scripts
chmod 755 /home/ubuntu/Maroik/Maroik.Crontab/*.sh

# check docker-compose.prod.yml file
COMPOSE_PROD_PATH="/home/ubuntu/Maroik/docker-compose.prod.yml"
if [ ! -f "$COMPOSE_PROD_PATH" ]; then
  echo "[INFO] Deployment is not set up yet: $COMPOSE_PROD_PATH not found."
  echo "[INFO] Set up the deployment files first."
  exit 1
fi

# .env.prod, its TUNNEL_TOKEN (for the Cloudflare Tunnel) and the Production Init.sql: checked in the preflight at the top.

# The compose files and the answers for the way the site is reached. Choice 2 runs docker-compose.prod.yml alone (the tunnel,
# .env.prod's domain), as AutoDeploy.sh does; choice 1 lays the mode's file over it. `sudo env` hands the answers to compose.
COMPOSE_FILES=(-f "$COMPOSE_PROD_PATH")
[ "$MODE" = "local" ] && COMPOSE_FILES+=(-f "$REPO_DIR/docker-compose.prod.$HOSTING.yml")
COMPOSE_ENV=(MAROIK_DOMAIN="$MAROIK_DOMAIN" MAROIK_ACME_EMAIL="${MAROIK_ACME_EMAIL:-}" MAROIK_HTTP_PORT="${MAROIK_HTTP_PORT:-}" MAROIK_SITE_URL="${MAROIK_SITE_URL:-}")
# The edge container of another mode, left from an earlier deploy, would keep the old way in open: remove it.
case "$HOSTING" in
  tunnel)      stale_edges="Maroik.Caddy" ;;
  portforward) stale_edges="Maroik.Cloudflared" ;;
  nodomain)    stale_edges="Maroik.Cloudflared Maroik.Caddy" ;;
esac

# Read external drive configuration from .env.prod
EXTERNAL_DRIVE_UUID=$(grep "^EXTERNAL_DRIVE_UUID=" "$ENV_PROD_PATH" | cut -d '=' -f2)
EXTERNAL_DRIVE_MOUNT=$(grep "^EXTERNAL_DRIVE_MOUNT=" "$ENV_PROD_PATH" | cut -d '=' -f2)
BACKUP_BASE_DIR=$(grep "^BACKUP_BASE_DIR=" "$ENV_PROD_PATH" | cut -d '=' -f2)

# Set default value if not specified
EXTERNAL_DRIVE_MOUNT="${EXTERNAL_DRIVE_MOUNT:-/home/ubuntu/512GB}"

# Check if external drive UUID is configured
if [ -z "$EXTERNAL_DRIVE_UUID" ] || [ "$EXTERNAL_DRIVE_UUID" == "<CHANGE_ME>" ]; then
  echo "[INFO] External drive UUID not configured in .env.prod"
  exit 1
else
  # Create mount point directory
  sudo mkdir -p "$EXTERNAL_DRIVE_MOUNT"

  # Check if external drive is already mounted
  if mountpoint -q "$EXTERNAL_DRIVE_MOUNT"; then
    echo "[INFO] External drive already mounted at $EXTERNAL_DRIVE_MOUNT"
  else
    # Try to mount the external drive
    echo "[INFO] Mounting external drive with UUID: $EXTERNAL_DRIVE_UUID"
    sudo mount UUID="$EXTERNAL_DRIVE_UUID" "$EXTERNAL_DRIVE_MOUNT"

    # Verify mount succeeded
    if ! mountpoint -q "$EXTERNAL_DRIVE_MOUNT"; then
      echo "[ERROR] Failed to mount external drive"
      echo "[ERROR] Please connect the external drive with UUID: $EXTERNAL_DRIVE_UUID"
      echo "[ERROR] Check device connection and run: sudo blkid"
      exit 1
    fi
    echo "[INFO] External drive mounted successfully at $EXTERNAL_DRIVE_MOUNT"
  fi

  if [ -z "$BACKUP_BASE_DIR" ]; then
    echo "[INFO] BACKUP_BASE_DIR not configured in .env.prod"
    exit 1
  fi

  # Create backup directory (Backup.sh stores Maroik-Backup-YYYY-MM-DD-HH-MM-SS.tar.xz here)
  sudo mkdir -p "$BACKUP_BASE_DIR"
fi

if [ "$MODE" = "github" ]; then
  # The app images CI published for this commit; docker-compose.prod.yml names them ...:${MAROIK_TAG}.
  # `sudo env` because sudo drops the caller's environment.
  # shellcheck disable=SC2086  # APP_SERVICES is a space-separated list on purpose
  sudo env MAROIK_TAG="$MAROIK_TAG" $COMPOSE -f "$COMPOSE_PROD_PATH" pull $APP_SERVICES \
    || { echo "[ERROR] Could not pull the $MAROIK_TAG images. Did the CI publish job push them, and are the packages public?"; exit 1; }
  # shellcheck disable=SC2086  # stale_edges is a space-separated list on purpose
  sudo docker rm -f $stale_edges > /dev/null 2>&1
  sudo env MAROIK_TAG="$MAROIK_TAG" "${COMPOSE_ENV[@]}" $COMPOSE "${COMPOSE_FILES[@]}" up -d

  # AutoDeploy.sh deploys only commits after this one. Through sudo: AutoDeploy.sh runs as root (cron) and may
  # have written these files last.
  echo "$DEPLOY_SHA" | sudo tee "$REPO_DIR/.deployed-sha" > /dev/null
  sudo rm -f "$REPO_DIR/.failed-sha"
else
  # Build the three app images here, the way the CI publish job builds them (release build: minified site.js, no source
  # maps), under the names docker-compose.prod.yml gives them (ghcr.io/ikhyeonjo/maroik-*:${MAROIK_TAG}). They carry the
  # same source label as the CI images, so the image prune below (in either mode) drops the old ones of both kinds.
  for app in website filestorage worker; do
    case $app in
      website)     dockerfile=Maroik.Website/Dockerfile ;;
      filestorage) dockerfile=Maroik.FileStorage/Dockerfile ;;
      worker)      dockerfile=Maroik.Worker/Dockerfile ;;
    esac
    echo "[INFO] Building maroik-$app:$MAROIK_TAG"
    sudo docker build -f "$REPO_DIR/$dockerfile" \
      --build-arg APP_VERSION="$APP_VERSION" --build-arg SOURCE_REVISION="$SOURCE_REVISION" \
      --label org.opencontainers.image.source="https://github.com/IkhyeonJo/Maroik" \
      --label org.opencontainers.image.version="$APP_VERSION" \
      --label org.opencontainers.image.revision="$SOURCE_REVISION" \
      -t "ghcr.io/ikhyeonjo/maroik-$app:$MAROIK_TAG" "$REPO_DIR" \
      || { echo "[ERROR] Building maroik-$app failed; nothing was deployed."; exit 1; }
  done

  # Start Docker Compose with the images just built (nothing is pulled for them: they exist locally under that tag).
  # `sudo env` because sudo drops the caller's environment.
  # shellcheck disable=SC2086  # stale_edges is a space-separated list on purpose
  sudo docker rm -f $stale_edges > /dev/null 2>&1
  sudo env MAROIK_TAG="$MAROIK_TAG" "${COMPOSE_ENV[@]}" $COMPOSE "${COMPOSE_FILES[@]}" up -d \
    || { echo "[ERROR] docker compose up failed."; exit 1; }
fi

# Drop the images earlier deploys left behind. Images in use by the running containers are kept. The earlier app images
# are still tagged, so a plain `prune` (dangling images only) never removes them: first every Maroik image no container
# uses (the source label), then the dangling rest. Build cache older than a week goes too, so the disk does not fill up
# while recent layers keep the next build fast.
sudo docker image prune -af --filter "label=org.opencontainers.image.source=https://github.com/IkhyeonJo/Maroik"
sudo docker image prune -f
sudo docker builder prune -f --filter until=168h

# Set up cron jobs: backup and resource monitoring, and — when production follows GitHub's main — AutoDeploy.sh. A build made
# here drops it (it would replace this build with GitHub's main).
if [ "$MODE" = "github" ]; then
  sudo crontab /home/ubuntu/Maroik/Maroik.Crontab/InitCron
else
  grep -v 'AutoDeploy.sh' /home/ubuntu/Maroik/Maroik.Crontab/InitCron | sudo crontab -
fi

# Reload cron service
sudo systemctl reload cron

# Inbound ports by the way the site is reached: none for the Cloudflare Tunnel (cloudflared dials out), 80 / 443 for port
# forwarding (Caddy), the chosen port without a domain. The server is managed physically (no SSH port).
sudo apt install ufw -y

echo "[+] Resetting firewall rules..."
sudo ufw --force reset
sudo ufw default deny incoming
sudo ufw default allow outgoing
case "$HOSTING" in
  portforward)
    sudo ufw allow 80/tcp
    sudo ufw allow 443/tcp
    sudo ufw allow 443/udp
    ;;
  nodomain)
    sudo ufw allow "$MAROIK_HTTP_PORT/tcp"
    ;;
esac

# Ping (ICMP Echo Request) block
if ! grep -q '^net.ipv4.icmp_echo_ignore_all = 1' /etc/sysctl.conf; then
  sudo tee -a /etc/sysctl.conf > /dev/null <<EOF

# Ping block (ICMP Echo Request ignore)
net.ipv4.icmp_echo_ignore_all = 1
EOF
fi

# Apply sysctl settings
sudo sysctl -p

# Enable UFW
sudo ufw --force enable

# Adds the current user to the "docker" group,
sudo usermod -a -G docker $USER

case "$HOSTING" in
  portforward)
    echo "[INFO] Port forwarding: forward 80 and 443 (TCP, and UDP 443) on the router to this server, and point $MAROIK_DOMAIN and"
    echo "[INFO] www.$MAROIK_DOMAIN (DNS A / AAAA) at its public IP. Caddy gets the certificate once both reach it."
    ;;
  nodomain)
    echo "[INFO] The site is at $MAROIK_SITE_URL (plain HTTP)."
    ;;
esac

# Starts a new shell session with the "docker" group applied.
newgrp docker
