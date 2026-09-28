#!/usr/bin/env bash
# Copy generated content from the gitignored staging mirror into the Unity project.
# Paths are stable, so re-running keeps the Unity-minted .meta/GUIDs of already imported files.
# Editor scripts in Tools/Staging/EditorScripts are NOT copied (their owners move them during integration).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
rsync -a --itemize-changes \
  --exclude '_work/' --exclude '.cache/' --exclude '.*' \
  "$ROOT/Tools/Staging/Assets/" "$ROOT/Starter/Assets/" | grep -v '^\.d' || true
