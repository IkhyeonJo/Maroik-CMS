#!/bin/bash

# Bring down the Docker containers defined in docker-compose.prod.yml. MAROIK_TAG only has to be set for the file to
# parse (its app images have no default tag); `down` removes the containers whatever image they run. --remove-orphans also
# removes the containers of a mode file Deploy.sh laid over it (Caddy of docker-compose.prod.portforward.yml).
if sudo docker compose version > /dev/null 2>&1; then
  sudo env MAROIK_TAG=down docker compose -f /home/ubuntu/Maroik/docker-compose.prod.yml down --remove-orphans
else
  sudo env MAROIK_TAG=down docker-compose -f /home/ubuntu/Maroik/docker-compose.prod.yml down --remove-orphans
fi

# Stop and remove all Docker containers
sudo docker stop $(sudo docker ps -a -q)
sudo docker rm $(sudo docker ps -a -q)

# Remove all Docker volumes
sudo docker volume rm $(sudo docker volume ls -q)

# Remove all Docker images. The app images come back on the next deploy (Deploy.sh builds them here or, with choice 2,
# pulls them from ghcr.io), the others from Docker Hub.
sudo docker rmi $(sudo docker images -q)

# Remove what `docker rmi` leaves behind: the build cache of Deploy.sh's builds, dangling
# images and unused networks, so the root disk does not fill up.
sudo docker builder prune -af
sudo docker system prune -af --volumes

# Remove root's cron jobs (backup, resource monitoring, AutoDeploy.sh), so nothing redeploys while the stack is down.
# Deploy.sh installs them again.
sudo crontab -r

# Disable ufw
sudo ufw disable

# unmount external drive
if [ -f /home/ubuntu/Maroik/.env.prod ]; then
    EXTERNAL_DRIVE_MOUNT=$(grep '^EXTERNAL_DRIVE_MOUNT=' /home/ubuntu/Maroik/.env.prod | cut -d '=' -f2)
    if [ -n "$EXTERNAL_DRIVE_MOUNT" ]; then
        sudo umount "$EXTERNAL_DRIVE_MOUNT"
    fi
fi