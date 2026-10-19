#!/bin/bash

# =============================================================================
# Maroik Backup Script
# =============================================================================
# Purpose: Collect the deployment files and the server-only data from the running server and
#          copy them to external drive as $BACKUP_BASE_DIR/Maroik-Backup-YYYY-MM-DD-HH-MM-SS.tar.xz
#
#          Maroik-Backup-YYYY-MM-DD-HH-MM-SS/
#            ├─ .env.prod
#            ├─ docker-compose.prod.yml
#            ├─ Maroik.Crontab/                                     (Backup.sh, InitCron, ResourceInfo.sh)
#            ├─ Maroik.DeployOps/                                   (Deploy.sh, Undeploy.sh)
#            ├─ Maroik.DB/PostgreSQL/SQL_Init_Script/Production/Init.sql   (pg_dump of Maroik.PostgreSQL)
#            └─ Maroik.FileStorage/upload/                          (Maroik.FileStorage:/app/upload)
#
#          Only .env.prod, Init.sql and upload/ are server-only (git-ignored in the Maroik repository);
#          the compose file and scripts are tracked there too, but are kept here so the archive is
#          self-contained. Extracting it over a Maroik checkout and running Deploy.sh restores the
#          server (its checkout replaces the tracked copies with the deployed commit's version).
# External drive capacity check: Delete oldest backup when usage exceeds 50%
# =============================================================================

set -e

# Configuration
MAROIK_ROOT_DIR="/home/ubuntu/Maroik"
ENV_FILE="$MAROIK_ROOT_DIR/.env.prod"

# Files copied as-is from $MAROIK_ROOT_DIR (.env.prod is server-only; the rest are tracked in the Maroik repository)
PRIVATE_FILES=(
    ".env.prod"
    "docker-compose.prod.yml"
    "Maroik.Crontab/Backup.sh"
    "Maroik.Crontab/InitCron"
    "Maroik.Crontab/ResourceInfo.sh"
    "Maroik.DeployOps/Deploy.sh"
    "Maroik.DeployOps/Undeploy.sh"
)
DB_DUMP_PATH="Maroik.DB/PostgreSQL/SQL_Init_Script/Production/Init.sql"
UPLOAD_PATH="Maroik.FileStorage/upload"

log() {
    echo "[$1] $(date '+%Y-%m-%d %H:%M:%S') - $2"
}

# Check .env.prod file exists
if [ ! -f "$ENV_FILE" ]; then
    log ERROR ".env.prod file not found: $ENV_FILE"
    exit 1
fi

# Read configuration from .env.prod
POSTGRES_USER=$(grep "^POSTGRES_USER=" "$ENV_FILE" | cut -d '=' -f2)
POSTGRES_PASSWORD=$(grep "^POSTGRES_PASSWORD=" "$ENV_FILE" | cut -d '=' -f2)
POSTGRES_DB=$(grep "^POSTGRES_DB=" "$ENV_FILE" | cut -d '=' -f2)
BACKUP_BASE_DIR=$(grep "^BACKUP_BASE_DIR=" "$ENV_FILE" | cut -d '=' -f2)
EXTERNAL_DRIVE_MOUNT=$(grep "^EXTERNAL_DRIVE_MOUNT=" "$ENV_FILE" | cut -d '=' -f2)

# Set defaults
POSTGRES_USER="${POSTGRES_USER:-postgres}"
POSTGRES_DB="${POSTGRES_DB:-Maroik}"
EXTERNAL_DRIVE_MOUNT="${EXTERNAL_DRIVE_MOUNT:-/home/ubuntu/512GB}"

# Local backup folder: the layout above is built in /home/ubuntu/Maroik-Backup, then compressed
# to /home/ubuntu/Maroik-Backup-YYYY-MM-DD-HH-MM-SS.tar.xz and copied to external drive.
# Locally only the latest .tar.xz is kept (the build folder is removed after compression).
# It sits next to the Maroik folder (not inside it), so it is never part of the
# Maroik checkout or its docker build context.
WORK_DIR="$(dirname "$MAROIK_ROOT_DIR")"
STAGING_NAME="Maroik-Backup"
STAGING_DIR="$WORK_DIR/$STAGING_NAME"
BACKUP_NAME="$STAGING_NAME-$(date '+%Y-%m-%d-%H-%M-%S')"
BACKUP_FILE="$WORK_DIR/${BACKUP_NAME}.tar.xz"

# Clean up a build folder left by an interrupted run
sudo rm -rf "$STAGING_DIR"

mkdir -p "$STAGING_DIR"

# Step 1: Copy private files
log INFO "Copying private files..."
for FILE in "${PRIVATE_FILES[@]}"; do
    if [ -f "$MAROIK_ROOT_DIR/$FILE" ]; then
        mkdir -p "$STAGING_DIR/$(dirname "$FILE")"
        cp -p "$MAROIK_ROOT_DIR/$FILE" "$STAGING_DIR/$FILE"
    else
        log WARNING "File not found, skipped: $MAROIK_ROOT_DIR/$FILE"
    fi
done

# Step 2: Dump database to Init.sql
log INFO "Dumping PostgreSQL database..."
mkdir -p "$STAGING_DIR/$(dirname "$DB_DUMP_PATH")"

# Escape special characters in password
POSTGRES_PASSWORD_ESCAPED=$(echo "$POSTGRES_PASSWORD" | sed 's/!/\\!/g' | sed 's/@/\\@/g')

if ! sudo docker exec -i Maroik.PostgreSQL /bin/bash -c \
    "PGPASSWORD='${POSTGRES_PASSWORD_ESCAPED}' pg_dump --username ${POSTGRES_USER} \"${POSTGRES_DB}\"" \
    > "$STAGING_DIR/$DB_DUMP_PATH"; then
    log ERROR "Database dump failed"
    sudo rm -rf "$STAGING_DIR"
    exit 1
fi
log SUCCESS "Database dump completed ($(du -h "$STAGING_DIR/$DB_DUMP_PATH" | cut -f1))"

# Step 3: Copy upload files
# Maroik.FileStorage:/app/upload is a bind mount of this host folder (docker-compose.prod.yml),
# so read it directly; the running container and the source folder are never modified.
log INFO "Copying FileStorage upload files..."
mkdir -p "$STAGING_DIR/$(dirname "$UPLOAD_PATH")"
if [ -d "$MAROIK_ROOT_DIR/$UPLOAD_PATH" ]; then
    sudo cp -a "$MAROIK_ROOT_DIR/$UPLOAD_PATH" "$STAGING_DIR/$UPLOAD_PATH"
    log SUCCESS "FileStorage upload files copied"
else
    log WARNING "FileStorage upload directory not found: $MAROIK_ROOT_DIR/$UPLOAD_PATH"
fi

# Step 4: Archive (lowest CPU/IO priority so the running services are not slowed down)
log INFO "Creating backup archive..."
# The folder inside the archive gets the same timestamped name as the archive itself
sudo nice -n 19 ionice -c3 tar cfJ "$BACKUP_FILE" \
    --transform "s,^$STAGING_NAME,$BACKUP_NAME," \
    -C "$WORK_DIR" "$STAGING_NAME"
sudo rm -rf "$STAGING_DIR"

log SUCCESS "Backup archive created: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"

# Keep only the latest archive locally: remove older ones now that the new one exists
find "$WORK_DIR" -maxdepth 1 -type f -name "Maroik-Backup-*.tar.xz" ! -name "$(basename "$BACKUP_FILE")" -delete

# Step 5: Copy to external drive (if configured and mounted)
log INFO "Preparing to copy backup to external drive..."

if [ -z "$BACKUP_BASE_DIR" ]; then
    log INFO "BACKUP_BASE_DIR not configured, skipping external drive backup"
    exit 1
fi

if ! mountpoint -q "$EXTERNAL_DRIVE_MOUNT"; then
    log WARNING "External drive not mounted: $EXTERNAL_DRIVE_MOUNT"
    echo "[INFO] Local backup kept at: $BACKUP_FILE"
    exit 1
fi

mkdir -p "$BACKUP_BASE_DIR"

# Check disk usage (50% threshold)
USAGE=$(df "$EXTERNAL_DRIVE_MOUNT" | tail -1 | awk '{print $5}' | sed 's/%//')
THRESHOLD=50

log INFO "External drive disk usage: ${USAGE}%"

if [ "$USAGE" -gt "$THRESHOLD" ]; then
    log WARNING "Disk usage exceeds ${THRESHOLD}%"

    # Find and delete oldest backup
    OLDEST=$(find "$BACKUP_BASE_DIR" -maxdepth 1 -type f -name "Maroik-Backup-*.tar.xz" | sort | head -1)

    if [ -n "$OLDEST" ]; then
        log INFO "Deleting oldest backup: $OLDEST"
        rm -f "$OLDEST"
    else
        log WARNING "No old backups found to delete"
    fi
fi

if cp "$BACKUP_FILE" "$BACKUP_BASE_DIR/"; then
    log SUCCESS "Backup copied to external drive: $BACKUP_BASE_DIR/$(basename "$BACKUP_FILE")"
    echo "[INFO] Latest local backup: $BACKUP_FILE"
else
    log ERROR "Failed to copy backup to external drive"
    echo "[INFO] Local backup kept at: $BACKUP_FILE"
    exit 1
fi

log INFO "Backup script completed"
