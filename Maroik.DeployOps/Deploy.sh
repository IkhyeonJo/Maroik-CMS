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

# ── Preflight: the server-only files this repository does not carry ─────────────────────────────────────────────────
# Checked first, before git, apt or docker do anything, and reported all at once.
ENV_PROD_PATH="$REPO_DIR/.env.prod"
SQL_PROD_PATH="$REPO_DIR/Maroik.DB/PostgreSQL/SQL_Init_Script/Production/Init.sql"
UPLOAD_PATH="$REPO_DIR/Maroik.FileStorage/upload"
missing=()

if [ ! -f "$ENV_PROD_PATH" ]; then
  missing+=(".env.prod not found: copy .env.prod.example to $ENV_PROD_PATH and fill in the real values.")
else
  env_value() { grep "^$1=" "$ENV_PROD_PATH" | head -1 | cut -d '=' -f2- | tr -d '\r'; }
  [ -n "$(env_value TUNNEL_TOKEN)" ] \
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

# .env.prod, its TUNNEL_TOKEN (cloudflared is the site's only way in) and the Production Init.sql: checked in the
# preflight at the top.

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
  sudo env MAROIK_TAG="$MAROIK_TAG" $COMPOSE -f "$COMPOSE_PROD_PATH" up -d

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
  sudo env MAROIK_TAG="$MAROIK_TAG" $COMPOSE -f "$COMPOSE_PROD_PATH" up -d \
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

# No inbound port at all: the site is reached only through the Cloudflare Tunnel, which
# cloudflared opens outbound (docker-compose.prod.yml), and the server is managed physically.
sudo apt install ufw -y

echo "[+] Resetting firewall rules..."
sudo ufw --force reset
sudo ufw default deny incoming
sudo ufw default allow outgoing

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

# Starts a new shell session with the "docker" group applied.
newgrp docker
