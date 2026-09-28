#!/usr/bin/env bash
# Serialize use of the single shared Unity Editor between parallel agents.
# Usage: Tools/editor_lock.sh <owner-label> <command...>
#   Waits for the lock (mkdir is atomic), runs the command, always releases the lock.
#   A lock older than 20 minutes is considered stale and broken.
set -u
LOCK_DIR="/tmp/billiard_rogue_editor.lock"
OWNER="${1:?owner label required}"; shift
while ! mkdir "$LOCK_DIR" 2>/dev/null; do
  if [ -n "$(find "$LOCK_DIR" -maxdepth 0 -mmin +20 2>/dev/null)" ]; then
    echo "editor_lock: breaking stale lock held by $(cat "$LOCK_DIR/owner" 2>/dev/null)" >&2
    rm -rf "$LOCK_DIR"; continue
  fi
  sleep 3
done
echo "$OWNER $$ $(date +%s)" > "$LOCK_DIR/owner"
trap 'rm -rf "$LOCK_DIR"' EXIT
"$@"
