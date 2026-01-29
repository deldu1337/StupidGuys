#!/usr/bin/env bash
set -euo pipefail

export GAME_SERVER_HOST=0.0.0.0
export GAME_SERVER_PORT=7778
export USE_PLAYFAB_GSDK=false

BINARY_PATH="/home/ubuntu/opt/stupidguys/gameserver/StupidGuysServer.x86_64"
LOG_FILE="run7778.log"
NOHUP_OUT="nohup.out"
RESTART_DELAY_SECONDS=2
SCENE_MISSING_PATTERN="StupidGuysRewardScene"

start_server() {
  nohup "$BINARY_PATH" -batchmode -nographics -port "$GAME_SERVER_PORT" -logFile "$LOG_FILE" \
    > "$NOHUP_OUT" 2>&1 &
  echo $!
}

while true; do
  server_pid=$(start_server)
  echo "[$(date -Is)] Started server (pid=$server_pid) on port $GAME_SERVER_PORT"

  if tail -n0 -F "$LOG_FILE" | grep -m1 "$SCENE_MISSING_PATTERN"; then
    echo "[$(date -Is)] Detected missing scene: $SCENE_MISSING_PATTERN. Restarting server."
    kill -TERM "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  else
    echo "[$(date -Is)] Log monitor stopped unexpectedly. Restarting server."
  fi

  sleep "$RESTART_DELAY_SECONDS"
  : > "$LOG_FILE"
  : > "$NOHUP_OUT"
  echo "[$(date -Is)] Restarting server loop."
done
