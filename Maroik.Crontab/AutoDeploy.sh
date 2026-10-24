#!/bin/bash

# Code-only auto deploy: Maroik.Crontab/AutoDeploy.sh, run by root's cron every 5 minutes (Maroik.Crontab/InitCron,
# installed by Deploy.sh's choice 2 (GitHub), removed by Undeploy.sh and by Deploy.sh's choice 1, which builds locally).
#
# CI (build-and-test -> publish) builds the Website, FileStorage and Worker images for every commit on main and pushes
# them to ghcr.io tagged sha-<12-char commit>. This script deploys the newest main commit whose publish job succeeded:
# it pulls those three images and replaces their containers. Nothing is built here, so production runs exactly the
# images CI built and tested. PostgreSQL, Valkey, RabbitMQ, ClamAV and cloudflared are not pulled, so they keep running,
# the database keeps its data, Init.sql is not run again and signed-in users stay signed in (sessions live in Valkey).
#
# A commit that changes Maroik.DB/ (Init.sql) is NOT deployed: that needs the manual
# Maroik.DeployOps/Undeploy.sh -> Deploy.sh (choice 2) -> restore with the new columns. It is logged, and later commits wait behind it until
# Deploy.sh (choice 2) has run (it writes .deployed-sha).

REPO_DIR="/home/ubuntu/Maroik"
COMPOSE_FILE="$REPO_DIR/docker-compose.prod.yml"
LOG="$REPO_DIR/Maroik.Log/AutoDeploy.log"
STATE_FILE="$REPO_DIR/.deployed-sha"
FAILED_FILE="$REPO_DIR/.failed-sha"
# The app services in docker-compose.prod.yml whose image is ghcr.io/ikhyeonjo/maroik-*:${MAROIK_TAG}.
APP_SERVICES="maroik.website maroik.filestorage maroik.worker"

# Cron runs this as root (InitCron is root's crontab), but the clone belongs to ubuntu. Git as ubuntu, so git does not
# refuse the repository ("dubious ownership") and .git never gets root-owned files that would break Deploy.sh (choice 2) later.
GIT="sudo -u ubuntu git -C $REPO_DIR"

log() { echo "[$(date '+%F %T')] $*" >> "$LOG"; }

# One run at a time: a pull can take longer than the 5-minute cron interval.
exec 9> /tmp/maroik-autodeploy.lock
flock -n 9 || exit 0

cd "$REPO_DIR" || exit 1

# Not a deployed clone yet (docker-compose.prod.yml comes with the checkout Deploy.sh (choice 2) makes). Echo for a manual run;
# log once for cron.
if [ ! -f "$COMPOSE_FILE" ]; then
  MSG="Deployment is not set up yet: $COMPOSE_FILE not found. Set up the deployment files first."
  echo "[INFO] $MSG"
  mkdir -p "$(dirname "$LOG")"
  grep -qF "$MSG" "$LOG" 2>/dev/null || log "$MSG"
  exit 1
fi

# No baseline yet: the first deploy after switching to the git clone is a manual Deploy.sh (choice 2).
[ -f "$STATE_FILE" ] || exit 0
CURRENT_SHA=$(cat "$STATE_FILE")

$GIT fetch -q origin main || { log "git fetch failed"; exit 1; }
NEW_SHA=$($GIT rev-parse origin/main)
[ "$NEW_SHA" = "$CURRENT_SHA" ] && exit 0
# A commit whose deploy already failed is not retried every 5 minutes; the next commit is.
[ "$NEW_SHA" = "$(cat "$FAILED_FILE" 2>/dev/null)" ] && exit 0

# Only a commit whose images CI published (publish runs after build-and-test passed). Still running -> next cron run.
CI_STATE=$(curl -fsS "https://api.github.com/repos/IkhyeonJo/Maroik/commits/$NEW_SHA/check-runs" \
  | python3 -c "import sys,json; r=[c for c in json.load(sys.stdin)['check_runs'] if c['name']=='publish']; print((r[0]['conclusion'] or r[0]['status']) if r else 'none')")
case "$CI_STATE" in
  success) ;;
  queued|in_progress|none|"") exit 0 ;;
  *) grep -q "CI for $NEW_SHA" "$LOG" 2>/dev/null || log "CI for $NEW_SHA: publish is '$CI_STATE'. Not deploying."; exit 0 ;;
esac

# Schema change -> manual deploy. Logged once per commit.
if ! $GIT diff --quiet "$CURRENT_SHA" "$NEW_SHA" -- Maroik.DB/; then
  grep -q "$NEW_SHA" "$LOG" 2>/dev/null || \
    log "Maroik.DB/ changed between $CURRENT_SHA and $NEW_SHA. Run Maroik.DeployOps/Undeploy.sh -> Deploy.sh (choice 2) by hand."
  exit 0
fi

# Keep the clone at the deployed commit, so the next run diffs Maroik.DB/ from here.
if ! $GIT checkout -q --detach "$NEW_SHA"; then
  # A tracked file edited on this box. Logged once; Deploy.sh (choice 2) (whose checkout overwrites such edits) clears it.
  grep -q "checkout $NEW_SHA failed" "$LOG" 2>/dev/null || \
    log "git checkout $NEW_SHA failed: a tracked file was changed on this box (git status). Fix it or run Deploy.sh (choice 2)."
  exit 1
fi

# Compose v2 (docker compose) when installed: v1 (docker-compose 1.29) fails with KeyError 'ContainerConfig' when it
# recreates a container on recent Docker Engine versions, which is exactly what this script does.
if docker compose version > /dev/null 2>&1; then
  COMPOSE="docker compose"
else
  COMPOSE="docker-compose"
fi

export MAROIK_TAG="sha-${NEW_SHA:0:12}"
log "Deploying $NEW_SHA ($MAROIK_TAG, was $CURRENT_SHA)"
# shellcheck disable=SC2086  # APP_SERVICES is a space-separated list on purpose
if $COMPOSE -f "$COMPOSE_FILE" pull $APP_SERVICES >> "$LOG" 2>&1 \
   && $COMPOSE -f "$COMPOSE_FILE" up -d --no-deps $APP_SERVICES >> "$LOG" 2>&1; then
  echo "$NEW_SHA" > "$STATE_FILE"
  # The previous deploys' app images are still tagged (sha-<commit>), so a plain `prune` (dangling images only) never
  # removes them: drop every Maroik image no container uses (the label CI puts on them), then the dangling rest.
  docker image prune -af --filter "label=org.opencontainers.image.source=https://github.com/IkhyeonJo/Maroik" > /dev/null 2>&1
  docker image prune -f > /dev/null 2>&1
  log "Deployed $NEW_SHA"
else
  # A failed pull leaves the running containers alone, so the site stays up on the previous version.
  echo "$NEW_SHA" > "$FAILED_FILE"
  log "Deploy of $NEW_SHA failed; see the compose output above. Not retried; a newer commit or Deploy.sh (choice 2) is."
  exit 1
fi
