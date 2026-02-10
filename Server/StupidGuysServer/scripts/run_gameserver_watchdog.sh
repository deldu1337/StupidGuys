#!/usr/bin/env bash
set -euo pipefail

GAME_SERVER_BIN="${GAME_SERVER_BIN:-/home/ubuntu/opt/stupidguys/gameserver/allocation/StupidGuysServer.x86_64}"
GAME_SERVER_HOST="${GAME_SERVER_HOST:-0.0.0.0}"
USE_PLAYFAB_GSDK="${USE_PLAYFAB_GSDK:-false}"
read -r -a PORTS <<< "${GAME_SERVER_PORTS:-7778 7779}"
RESTART_DELAY_SECONDS="${RESTART_DELAY_SECONDS:-2}"
LOG_DIR="${LOG_DIR:-$(pwd)/logs}"

mkdir -p "$LOG_DIR"

start_server() {
    local port="$1"
    local nohup_log="$LOG_DIR/nohup_${port}.out"
    local run_log="$LOG_DIR/run_${port}.log"

    echo "[watchdog] starting game server on port ${port}"
    (
        export GAME_SERVER_HOST="$GAME_SERVER_HOST"
        export GAME_SERVER_PORT="$port"
        export USE_PLAYFAB_GSDK="$USE_PLAYFAB_GSDK"
        nohup "$GAME_SERVER_BIN" \
            -batchmode -nographics \
            -port "$port" \
            -logFile "$run_log" \
            >>"$nohup_log" 2>&1 &
    )
}

is_server_running() {
    local port="$1"
    pgrep -f "${GAME_SERVER_BIN}.*-port ${port}" >/dev/null 2>&1
}

main_loop() {
    while true; do
        for port in "${PORTS[@]}"; do
            if ! is_server_running "$port"; then
                start_server "$port"
            fi
        done

        sleep "$RESTART_DELAY_SECONDS"
    done
}

main_loop
