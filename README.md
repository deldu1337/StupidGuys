````md
# StupidGuys EC2 배포 가이드

## EC2 디렉토리 구조

/home/ubuntu/opt/stupidguys/
├─ tcpchat/                      # TCP 채팅 서버
│  ├─ TCPChat.x86_64              # 빌드 결과물
│  ├─ TCPChat_Data/               # 빌드 결과물
│  ├─ TCPChat.log                 # TCP 채팅 서버 로그
│  ├─ run_tcpchat.sh              # TCP 채팅 서버 실행 스크립트
│  └─ stop_tcpchat.sh             # TCP 채팅 서버 중지 스크립트
├─ matchmaking/                   # ASP.NET Core SignalR 서버
│  ├─ StupidGuysServer/           # publish 결과물
│  └─ logs/                       # Dedicated Server 로그
│     ├─ run_7778.log              # 7778번 포트 서버 프로세스 로그
│     └─ watchdog_7778.out         # Watchdog 프로세스 로그
│  ├─ nohup.out                   # 매치메이킹 로그
│  ├─ run_gameserver_watchdog.sh  # Dedicated Server 실행 스크립트(Watchdog)
│  ├─ run_matchmaking.sh          # 매치메이킹 실행 스크립트
│  ├─ stop_gameserver_watchdog.sh # Dedicated Server 중지 스크립트(Watchdog)
│  └─ stop_matchmaking.sh         # 매치메이킹 중지 스크립트
├─ auth/                          # Auth API 서버
│  ├─ Auth/                       # Auth 서버 publish 결과물
│  ├─ run_auth.sh                 # Auth 서버 실행 스크립트
│  └─ stop_auth.sh                # Auth 서버 중지 스크립트
├─ postgres/                      # PostgreSQL DB (Docker)
│  ├─ docker-compose.yml          # docker 정의 파일
│  └─ data/                       # PostgreSQL 볼륨 데이터
└─ gameserver/                    # Unity Dedicated Server 빌드(2개 프로세스)
   └─ allocation/                 # Dedicated Server 빌드 결과물
      ├─ StupidGuysServer.x86_64    # 빌드 결과물
      ├─ StupidGuysServer_Data/     # 빌드 결과물
      └─ run_7778.log               # 7778번 포트 서버 프로세스 로그
   ├─ run_7778.sh                  # 7778번 포트 서버 프로세스 실행 스크립트
   └─ run_7779.sh                  # 7779번 포트 서버 프로세스 실행 스크립트(예비)
````

## EC2 인바운드 규칙 (Security Group)

```text
- SSH 접속: 22/TCP
- Auth 서버: 5000/TCP
- DB(PostgreSQL): 5432/TCP
- 채팅 서버: 7777/TCP
- 게임 서버: 7778-7779/UDP
- 매치메이킹 SignalR: 10000/TCP
```

---

## TCP 채팅 서버 배포 및 실행 절차

### 1) Unity에서 Linux Server 빌드

* Build Target: `Linux Server`
* Scene List: `Scenes/TCPChatServer`
* 생성 파일: `TCPChat.x86_64`, `TCPChat_Data`

### 2) EC2로 업로드

```bash
scp -r TCPChat.x86_64 TCPChat_Data ubuntu@<ElasticIP>:/home/ubuntu/opt/stupidguys/TCPChat/

# 또는 파일질라FTP로 전송
```

### 3) 실행/중지 스크립트 생성

```bash
cd /home/ubuntu/opt/stupidguys/TCPChat

vi run_tcpchat.sh
```

```bash
#!/usr/bin/env bash
nohup /home/ubuntu/opt/stupidguys/TCPChat/TCPChat.x86_64 -batchmode -nographics -port 7777 -logFile TCPChat.log > nohup.out 2>&1 &
```

```bash
vi stop_tcpchat.sh
```

```bash
#!/usr/bin/env bash
set -euo pipefail

PATTERN="/home/ubuntu/opt/stupidguys/TCPChat/TCPChat.x86_64"

pids=$(pgrep -f "$PATTERN" || true)
if [[ -z "${pids}" ]]; then
  echo "TCPChat: 실행 중인 프로세스가 없습니다"
  exit 0
fi

echo "TCPChat: 종료 대상 PID: ${pids}"
echo "${pids}" | xargs -r kill

for _ in {1..20}; do
  sleep 0.2
  if ! pgrep -f "$PATTERN" >/dev/null 2>&1; then
    echo "TCPChat: 정상 종료되었습니다"
    exit 0
  fi
done

echo "TCPChat: 아직 실행 중 -> 강제 종료"
pids=$(pgrep -f "$PATTERN" || true)
[[ -n "${pids}" ]] && echo "${pids}" | xargs -r kill -9
echo "TCPChat: 강제 종료 완료"
```

### 4) 실행 및 확인

```bash
chmod 755 /home/ubuntu/opt/stupidguys/TCPChat/TCPChat.x86_64
chmod 755 /home/ubuntu/opt/stupidguys/TCPChat/run_tcpchat.sh
chmod 755 /home/ubuntu/opt/stupidguys/TCPChat/stop_tcpchat.sh

./run_tcpchat.sh
ps -ef | grep 7777
ss -nltp | grep 7777
```

### 5) 중지 및 확인

```bash
./stop_tcpchat.sh
ps -ef | grep 7777
```

### 6) 로그 확인

```bash
# 전체 확인
view TCPChat.log

# 실시간 확인
tail -f TCPChat.log
```

---

## PostgreSQL 구성 절차 (Docker)

### 1) docker-compose.yml 구성

```bash
cd /home/ubuntu/opt/stupidguys/postgres
vi docker-compose.yml
```

```yaml
version: "3.9"
services:
  auth-postgres:
    image: postgres:16
    container_name: auth-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: stupidguys_db
      POSTGRES_USER: root
      POSTGRES_PASSWORD: "1234"
    ports:
      - "5432:5432"
    volumes:
      - /home/ubuntu/opt/stupidguys/postgres/data:/var/lib/postgresql/data
```

### 2) docker 명령어

```bash
# 컨테이너 생성/실행
sudo docker-compose up -d

# 컨테이너 상태 확인
sudo docker ps -a

# 컨테이너 중지
sudo docker stop [CONTAINER ID]

# 컨테이너 삭제
sudo docker rm [CONTAINER ID]

# postgreSQL 접속
sudo docker exec -it auth-postgres psql -U root -d stupidguys_db
```

### 3) postgreSQL 명령어

```sql
-- 테이블 목록 확인
\dt

-- 조회
select * from "[TABLE]";

-- 종료
\q
```

### 4) 프로세스 및 포트 확인

```bash
ps -ef | grep 5432
ss -nltp | grep 5432
```

---

## Auth 서버 배포 및 실행 절차

### 1) 로컬에서 마이그레이션

```bash
cd Server/Persistence/Auth
dotnet ef migrations add InitialCreate
```

### 2) 로컬에서 publish

```bash
dotnet publish -c Release -o publish
```

### 3) EC2로 업로드

```bash
scp -r publish/ ubuntu@<ElasticIP>:/home/ubuntu/opt/stupidguys/auth/Auth/

# 또는 파일질라FTP로 전송
```

### 4) Auth 실행/중지 스크립트 생성

```bash
cd /home/ubuntu/opt/stupidguys/auth

vi run_auth.sh
```

```bash
#!/usr/bin/env bash

# ===== 환경 변수 =====
export ASPNETCORE_URLS=http://0.0.0.0:5000
export DATABASE_URL="Host=127.0.0.1;Port=5432;Database=stupidguys_db;Username=root;Password=1234"

# ===== 실행 =====
nohup dotnet /home/ubuntu/opt/stupidguys/auth/Auth/Auth.dll \
  > /home/ubuntu/opt/stupidguys/auth/auth.out 2>&1 &
```

```bash
vi stop_auth.sh
```

```bash
#!/usr/bin/env bash
set -euo pipefail

PATTERN="dotnet /home/ubuntu/opt/stupidguys/auth/Auth/Auth.dll"

pids=$(pgrep -f "$PATTERN" || true)
if [[ -z "${pids}" ]]; then
  echo "Auth: 실행 중인 프로세스가 없습니다"
  exit 0
fi

echo "Auth: 종료 대상 PID: ${pids}"
echo "${pids}" | xargs -r kill

for _ in {1..20}; do
  sleep 0.2
  if ! pgrep -f "$PATTERN" >/dev/null 2>&1; then
    echo "Auth: 정상 종료되었습니다"
    exit 0
  fi
done

echo "Auth: 아직 실행 중 -> 강제 종료"
pids=$(pgrep -f "$PATTERN" || true)
[[ -n "${pids}" ]] && echo "${pids}" | xargs -r kill -9
echo "Auth: 강제 종료 완료"
```

### 5) 실행 및 확인

```bash
chmod 755 /home/ubuntu/opt/stupidguys/auth/run_auth.sh
chmod 755 /home/ubuntu/opt/stupidguys/auth/stop_auth.sh

./run_auth.sh
ps -ef | grep Auth.dll
ss -nltp | grep 5000
```

### 6) 중지

```bash
./stop_auth.sh
ps -ef | grep Auth.dll
```

### 7) 로그 확인

```bash
view auth.out
tail -f auth.out
```

---

## 매치메이킹 서버 배포 및 실행 절차

### 1) 로컬에서 publish

```bash
cd Server/StupidGuysServer/StupidGuysServer
dotnet publish -c Release -o publish
```

### 2) EC2로 업로드

```bash
scp -r publish/ ubuntu@<ElasticIP>:/home/ubuntu/opt/stupidguys/matchmaking/StupidGuysServer/

# 또는 파일질라FTP로 전송
```

### 3) 매치메이킹 실행/중지 스크립트 생성

```bash
cd /home/ubuntu/opt/stupidguys/matchmaking

vi run_matchmaking.sh
```

```bash
#!/usr/bin/env bash
export PORT=10000
export GAME_SERVER_HOST=3.37.215.9
export GAME_SERVER_PORT=7778
export ALLOCATION_PORT_START=7778
export ALLOCATION_PORT_END=7779

nohup dotnet /home/ubuntu/opt/stupidguys/matchmaking/StupidGuysServer/StupidGuysServer.dll /home/ubuntu/opt/stupidguys/matchmaking/matchmaking.log 2>&1 &
```

```bash
vi stop_matchmaking.sh
```

```bash
#!/usr/bin/env bash
set -euo pipefail

PATTERN="dotnet /home/ubuntu/opt/stupidguys/matchmaking/StupidGuysServer/StupidGuysServer.dll"

pids=$(pgrep -f "$PATTERN" || true)
if [[ -z "${pids}" ]]; then
  echo "Matchmaking: 실행 중인 프로세스가 없습니다"
  exit 0
fi

echo "Matchmaking: 종료 대상 PID: ${pids}"
echo "${pids}" | xargs -r kill

for _ in {1..20}; do
  sleep 0.2
  if ! pgrep -f "$PATTERN" >/dev/null 2>&1; then
    echo "Matchmaking: 정상 종료되었습니다"
    exit 0
  fi
done

echo "Matchmaking: 아직 실행 중 -> 강제 종료"
pids=$(pgrep -f "$PATTERN" || true)
[[ -n "${pids}" ]] && echo "${pids}" | xargs -r kill -9
echo "Matchmaking: 강제 종료 완료"
```

### 4) Dedicated Server 실행 스크립트 (Watchdog)

> 아래 Watchdog 스크립트는 **실제 운영/테스트에서 Dedicated Server를 기동하는 방식**이다.
> (7778만 기동)

```bash
cd /home/ubuntu/opt/stupidguys/matchmaking
vi run_gameserver_watchdog.sh
```

```bash
#!/usr/bin/env bash
set -euo pipefail

# ===== Config (override 가능) =====
GAME_SERVER_BIN="${GAME_SERVER_BIN:-/home/ubuntu/opt/stupidguys/gameserver/allocation/StupidGuysServer.x86_64}"
GAME_SERVER_HOST="${GAME_SERVER_HOST:-0.0.0.0}"
USE_PLAYFAB_GSDK="${USE_PLAYFAB_GSDK:-false}"

# 7778만 사용
PORTS=(7778)

RESTART_DELAY_SECONDS="${RESTART_DELAY_SECONDS:-2}"

BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="${LOG_DIR:-$BASE_DIR/logs}"

# 7778 전용 파일명으로 분리
PID_FILE="${PID_FILE:-$LOG_DIR/watchdog_7778.pid}"
WATCHDOG_OUT="${WATCHDOG_OUT:-$LOG_DIR/watchdog_7778.out}"

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
  pgrep -f "${GAME_SERVER_BIN}.*-port[[:space:]]+${port}" >/dev/null 2>&1
}

is_watchdog_running() {
  if [[ -f "$PID_FILE" ]]; then
    local pid
    pid="$(cat "$PID_FILE" 2>/dev/null || true)"
    [[ -n "${pid}" ]] && kill -0 "$pid" >/dev/null 2>&1 && return 0
  fi
  return 1
}

start_watchdog_bg() {
  if is_watchdog_running; then
    echo "[watchdog] already running (pid=$(cat "$PID_FILE"))"
    exit 0
  fi

  nohup env \
    GAME_SERVER_BIN="$GAME_SERVER_BIN" \
    GAME_SERVER_HOST="$GAME_SERVER_HOST" \
    USE_PLAYFAB_GSDK="$USE_PLAYFAB_GSDK" \
    RESTART_DELAY_SECONDS="$RESTART_DELAY_SECONDS" \
    LOG_DIR="$LOG_DIR" \
    PID_FILE="$PID_FILE" \
    WATCHDOG_OUT="$WATCHDOG_OUT" \
    "$0" run \
    >>"$WATCHDOG_OUT" 2>&1 &

  echo $! > "$PID_FILE"
  echo "[watchdog] started in background (pid=$(cat "$PID_FILE"))"
}

stop_watchdog() {
  if ! is_watchdog_running; then
    echo "[watchdog] not running"
    rm -f "$PID_FILE" 2>/dev/null || true
    exit 0
  fi

  local pid
  pid="$(cat "$PID_FILE")"
  echo "[watchdog] stopping watchdog pid=$pid"
  kill "$pid" 2>/dev/null || true

  for _ in {1..30}; do
    sleep 0.2
    if ! kill -0 "$pid" >/dev/null 2>&1; then
      rm -f "$PID_FILE" 2>/dev/null || true
      echo "[watchdog] stopped"
      exit 0
    fi
  done

  echo "[watchdog] still running, force kill"
  kill -9 "$pid" 2>/dev/null || true
  rm -f "$PID_FILE" 2>/dev/null || true
  echo "[watchdog] force-stopped"
}

status_watchdog() {
  if is_watchdog_running; then
    echo "[watchdog] running (pid=$(cat "$PID_FILE"))"
  else
    echo "[watchdog] not running"
  fi

  local port="7778"
  if is_server_running "$port"; then
    echo "[server] port ${port}: running"
    pgrep -af "${GAME_SERVER_BIN}.*-port[[:space:]]+${port}" || true
  else
    echo "[server] port ${port}: not running"
  fi
}

main_loop() {
  echo "[watchdog] run loop started"
  echo "[watchdog] port: 7778"
  echo "[watchdog] bin: $GAME_SERVER_BIN"
  echo "[watchdog] log_dir: $LOG_DIR"

  while true; do
    local port="7778"
    if ! is_server_running "$port"; then
      start_server "$port"
    fi
    sleep "$RESTART_DELAY_SECONDS"
  done
}

cmd="${1:-start}"
case "$cmd" in
  start) start_watchdog_bg ;;
  run) main_loop ;;
  stop) stop_watchdog ;;
  restart) stop_watchdog; start_watchdog_bg ;;
  status) status_watchdog ;;
  *) echo "Usage: $0 {start|stop|restart|status|run}"; exit 1 ;;
esac
```

### 5) Dedicated Server 중지 스크립트 (Watchdog)

```bash
cd /home/ubuntu/opt/stupidguys/matchmaking
vi stop_gameserver_watchdog.sh
```

```bash
#!/usr/bin/env bash
set -euo pipefail

GAME_SERVER_BIN="${GAME_SERVER_BIN:-/home/ubuntu/opt/stupidguys/gameserver/allocation/StupidGuysServer.x86_64}"

BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="${LOG_DIR:-$BASE_DIR/logs}"

PID_FILE="${PID_FILE:-$LOG_DIR/watchdog_7778.pid}"
PORT="7778"

stop_by_pidfile() {
  if [[ ! -f "$PID_FILE" ]]; then
    echo "[watchdog] pid file not found: $PID_FILE (already stopped?)"
    return 0
  fi

  local pid
  pid="$(cat "$PID_FILE" 2>/dev/null || true)"

  if [[ -z "${pid}" ]]; then
    echo "[watchdog] empty pid file -> remove"
    rm -f "$PID_FILE" 2>/dev/null || true
    return 0
  fi

  if ! kill -0 "$pid" >/dev/null 2>&1; then
    echo "[watchdog] not running (stale pid=$pid) -> remove pid file"
    rm -f "$PID_FILE" 2>/dev/null || true
    return 0
  fi

  echo "[watchdog] stopping watchdog pid=$pid"
  kill "$pid" 2>/dev/null || true

  for _ in {1..30}; do
    sleep 0.2
    if ! kill -0 "$pid" >/dev/null 2>&1; then
      rm -f "$PID_FILE" 2>/dev/null || true
      echo "[watchdog] stopped"
      return 0
    fi
  done

  echo "[watchdog] still running, force kill"
  kill -9 "$pid" 2>/dev/null || true
  rm -f "$PID_FILE" 2>/dev/null || true
  echo "[watchdog] force-stopped"
}

stop_server_7778() {
  local pids
  pids=$(pgrep -af "${GAME_SERVER_BIN}.*-port[[:space:]]+${PORT}" | awk '$2 ~ /StupidGuysServer\.x86_64/ {print $1}' || true)

  if [[ -z "${pids}" ]]; then
    pids=$(pgrep -x "StupidGuysServer.x86_64" || true)
    if [[ -n "${pids}" ]]; then
      pids=$(ps -o pid= -o args= -p ${pids} | awk '$0 ~ /-port[[:space:]]+7778/ {print $1}' || true)
    fi
  fi

  if [[ -z "${pids}" ]]; then
    echo "[server] 7778: no process found"
    return 0
  fi

  echo "[server] 7778: stopping PIDs: ${pids}"
  echo "${pids}" | xargs -r kill

  for _ in {1..30}; do
    sleep 0.2
    if ! pgrep -f "${GAME_SERVER_BIN}.*-port[[:space:]]+${PORT}" >/dev/null 2>&1; then
      echo "[server] 7778: stopped"
      return 0
    fi
  done

  echo "[server] 7778: still running, force kill"
  pids=$(pgrep -f "${GAME_SERVER_BIN}.*-port[[:space:]]+${PORT}" || true)
  [[ -n "${pids}" ]] && echo "${pids}" | xargs -r kill -9
  echo "[server] 7778: force-stopped"
}

stop_by_pidfile
stop_server_7778

echo "stop done"
```

### 6) 실행 및 확인

```bash
chmod 755 /home/ubuntu/opt/stupidguys/matchmaking/run_matchmaking.sh
chmod 755 /home/ubuntu/opt/stupidguys/matchmaking/stop_matchmaking.sh

./run_matchmaking.sh
ps -ef | grep matchmaking
ss -nltp | grep 10000
```

```bash
chmod 755 /home/ubuntu/opt/stupidguys/matchmaking/run_gameserver_watchdog.sh
chmod 755 /home/ubuntu/opt/stupidguys/matchmaking/stop_gameserver_watchdog.sh

./run_gameserver_watchdog.sh
ps -ef | grep 7778
```

### 7) 중지

```bash
./stop_matchmaking.sh
ps -ef | grep matchmaking

./stop_gameserver_watchdog.sh
ps -ef | grep 7778
```

### 8) 로그 확인

```bash
view nohup.out
tail -f nohup.out
```

---

## Unity Dedicated Server 배포 및 실행 절차

### 1) Unity에서 Linux Server 빌드

* Build Target: `Linux Server`
* Scene List: `Scenes/InGame`
* 생성 파일: `StupidGuysServer.x86_64`, `StupidGuysServer_Data`

### 2) EC2로 업로드

```bash
scp -r StupidGuysServer.x86_64 StupidGuysServer_Data ubuntu@<ElasticIP>:/home/ubuntu/opt/stupidguys/gameserver/allocation/

# 또는 파일질라FTP로 전송
```

### 3) Dedicated Server 직접 실행 스크립트 생성(직접 실행용)

```bash
cd /home/ubuntu/opt/stupidguys/gameserver

vi run_7778.sh
```

```bash
#!/usr/bin/env bash
export GAME_SERVER_HOST=0.0.0.0
export GAME_SERVER_PORT=7778
export USE_PLAYFAB_GSDK=false

nohup /home/ubuntu/opt/stupidguys/gameserver/allocation/StupidGuysServer.x86_64 -batchmode -nographics -port 7778 -logFile run7778.log > nohup.out 2>&1 &
```

```bash
vi stop_7778.sh
```

```bash
#!/usr/bin/env bash
set -euo pipefail

PATTERN="/home/ubuntu/opt/stupidguys/gameserver/allocation/StupidGuysServer.x86_64.*-port[[:space:]]*7778"

pids=$(pgrep -f "$PATTERN" || true)
if [[ -z "${pids}" ]]; then
  echo "Dedicated(7778): 실행 중인 프로세스가 없습니다"
  exit 0
fi

echo "Dedicated(7778): 종료 대상 PID: ${pids}"
echo "${pids}" | xargs -r kill

for _ in {1..20}; do
  sleep 0.2
  if ! pgrep -f "$PATTERN" >/dev/null 2>&1; then
    echo "Dedicated(7778): 정상 종료되었습니다"
    exit 0
  fi
done

echo "Dedicated(7778): 아직 실행 중 -> 강제 종료"
pids=$(pgrep -f "$PATTERN" || true)
[[ -n "${pids}" ]] && echo "${pids}" | xargs -r kill -9
echo "Dedicated(7778): 강제 종료 완료"
```

### 4) 실행 및 확인

```bash
chmod 755 /home/ubuntu/opt/stupidguys/gameserver/allocation/StupidGuysServer.x86_64
chmod 755 /home/ubuntu/opt/stupidguys/gameserver/run_7778.sh

./run_7778.sh
ps -ef | grep 7778
```

### 5) 중지

```bash
./stop_7778.sh
ps -ef | grep 7778
```

### 6) 로그 확인

```bash
view run_7778.log
tail -f run_7778.log
```

---

## Client 빌드 및 실행 절차

### 1) Unity에서 Windows 빌드

* Edit → Project Settings → Player → Other Settings → Configuration

  * Allow downloads over HTTP: `Always allowed`
* Build Target: `Windows`
* Scene List

  * `Scenes/Login/Auth`
  * `Scenes/MatchMaking/MatchMakingTestScene`
  * `Scenes/InGame`
  * `Scenes/Reward/StupidGuysRewardScene`
* 생성 파일: `StupidGuys.exe`, `UnityPlayer.dll`

### 2) 실행

```text
StupidGuys.exe 실행
```

### 3) 클라이언트 로그 확인

```text
<사용자>/AppData/LocalLow/DefaultCompany/StupidGuys/Plawwyer.log
```

---

## 진행 순서

### 실행 순서

```text
TCPChat -> postgres -> Auth -> Matchmaking -> Unity Dedicated Server -> Client
```

### 중지 순서

```text
Client -> Unity Dedicated Server -> Matchmaking -> Auth -> postgres -> TCPChat
```

---

## 참고사항

* `gameserver/` 디렉토리에도 Dedicated Server 실행 스크립트(`run_7778.sh`, `run_7779.sh`)가 존재하지만, **실제 운영/테스트 환경에서는 Dedicated Server를 직접 실행하지 않는다.**
* Dedicated Server 바이너리는 `gameserver/allocation/StupidGuysServer.x86_64`에 위치하지만, **기동은 `matchmaking/run_gameserver_watchdog.sh`(Watchdog)에서 수행하며 자동 재시작까지 포함한 운영 방식이다.**
* 또한 **실제 운영/테스트에서는 메모리 이슈로 인해 7779 포트는 기동하지 않고 7778 포트만 단일로 기동한다.**
