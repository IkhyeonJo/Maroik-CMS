#!/bin/bash

# Read .env.prod file
ENV_FILE="/home/ubuntu/Maroik/.env.prod"

if [ ! -f "$ENV_FILE" ]; then
  echo "[ERROR] Environment file $ENV_FILE not found."
  exit 1
fi

# Read MAROIK_LOG_DIR from .env.prod
MAROIK_LOG_DIR=$(grep '^MAROIK_LOG_DIR=' "$ENV_FILE" | cut -d '=' -f2)
if [ -z "$MAROIK_LOG_DIR" ]; then
  MAROIK_LOG_DIR="/home/ubuntu/Maroik/Maroik.Log"
fi

SHARED_DIR="$MAROIK_LOG_DIR"
HOST_OUTPUT_FILE="$SHARED_DIR/HostResourceInfo.txt"
DOCKER_OUTPUT_FILE="$SHARED_DIR/DockerResourceInfo.txt"

# Host CPU Information
echo "Host CPU Information:" > $HOST_OUTPUT_FILE
CPU_USAGE=$(awk -v INTERVAL=1 '
    BEGIN { getline; split($0, prev, " "); total_prev = 0; idle_prev = prev[5]; for (i = 2; i <= NF; i++) total_prev += prev[i] }
    { getline; split($0, curr, " "); total_curr = 0; idle_curr = curr[5]; for (i = 2; i <= NF; i++) total_curr += curr[i] }
    END { print 100 - ((idle_curr - idle_prev) * 100 / (total_curr - total_prev)) }
' <(grep 'cpu ' /proc/stat) <(sleep 1; grep 'cpu ' /proc/stat))
echo "CPU Usage: $CPU_USAGE%" >> $HOST_OUTPUT_FILE

# Host Memory Information (Total / Usage)
echo "Host Memory Information:" >> $HOST_OUTPUT_FILE
MEMORY_USAGE=$(LC_ALL=C free -h | awk '$1 == "Mem:" {print $2 " / " $3}')
echo "Memory: $MEMORY_USAGE" >> $HOST_OUTPUT_FILE

# Host Disk Information (Total / Usage) of the root filesystem, which holds the Docker volumes
# (PostgreSQL), the uploads and the logs. Summing every `df` row counted tmpfs, /boot/efi, the backup
# disk and one overlay mount per running container (each the same root disk again) several times over.
echo "Host Disk Information:" >> $HOST_OUTPUT_FILE
read -r TOTAL USED < <(df -B1 --output=size,used / | tail -n 1)
TOTAL_HUMAN=$(numfmt --to=iec --suffix=B "$TOTAL")
USED_HUMAN=$(numfmt --to=iec --suffix=B "$USED")
echo "Disk: $TOTAL_HUMAN / $USED_HUMAN" >> $HOST_OUTPUT_FILE

# Docker container status and resource usage information
echo "Docker Container Status:" > $DOCKER_OUTPUT_FILE
docker ps --format "table {{.Names}}\t{{.Status}}" >> $DOCKER_OUTPUT_FILE

echo "Docker Resource Usage:" >> $DOCKER_OUTPUT_FILE
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}" >> $DOCKER_OUTPUT_FILE
