# EC2 마이그레이션 가이드 (Matchmaking + Game Server + Auth/PostgreSQL)

이 문서는 기존 Render/PlayFab 기반에서 AWS EC2로 매치메이킹/게임 서버를 옮기는 절차와,
Auth 서버의 PostgreSQL DB를 **Render → EC2(도커 컨테이너)** 로 이전하는 흐름을 정리합니다.

## 1) Elastic IP를 어디에 넣나요?

### 매치메이킹 서버가 반환하는 게임 서버 주소
매치메이킹 서버는 새 로비를 만들 때 아래 환경 변수로 설정된 호스트/포트를 클라이언트에게 반환합니다.

- `GAME_SERVER_HOST`: **Elastic IP(공인 IP)** 입력
- `GAME_SERVER_PORT`: 게임 서버가 리스닝하는 포트 (예: 7777)

서버 코드에서 위 값을 읽어 로비 생성 시 응답으로 돌려줍니다.【F:Server/StupidGuysServer/StupidGuysServer/Configuration/GameServerSettings.cs†L1-L28】【F:Server/StupidGuysServer/StupidGuysServer/Hubs/MatchmakingHub.cs†L43-L76】

### 매치메이킹 종료 조건/포트 풀 환경 변수
- `MATCH_TIMEOUT_SECONDS`: 첫 플레이어 입장 후 매치 종료까지의 최대 대기 시간(기본 60초)
- `ALLOCATION_PORT_START` / `ALLOCATION_PORT_END`: EC2에서 미리 띄워둔 프로세스 포트 범위 (예: 7778~7798)

### Unity 클라이언트가 접속할 매치메이킹 URL
클라이언트는 매치메이킹 서버 주소를 아래 순서로 해석합니다.

1. 인스펙터에 지정된 `serverUrl`
2. `MATCHMAKING_SERVER_URL` 환경 변수
3. 기본값 `http://localhost:10000/matchmaking`

EC2에서 매치메이킹 서버를 서비스한다면, `MATCHMAKING_SERVER_URL`에 **EC2 Elastic IP**를 포함한 주소를 넣어주세요.
예: `http://<ElasticIP>:10000/matchmaking`【F:Unity/Assets/Scripts/MatchMaking/MMScripts/MatchmakingClient.cs†L8-L55】

## 2) EC2 준비 사항 (보안그룹/포트)

아래 포트를 인바운드로 허용해야 합니다.

- 매치메이킹 SignalR: `10000/tcp` (기본값, 필요 시 변경)
- 게임 서버(UTP): `7778-7798/udp` (20개 프로세스 포트 풀)
- 채팅 서버(TCP): 현재 사용 중인 포트

> 참고: 매치메이킹은 TCP, Unity UTP는 UDP를 사용합니다.

## 3) EC2 디렉토리 구조 예시

```
/opt/stupidguys/
├─ matchmaking/               # ASP.NET Core SignalR 서버
│  ├─ StupidGuysServer/        # publish 결과물
│  └─ run.sh                   # 실행 스크립트
├─ auth/                       # Auth API 서버
│  ├─ Auth/                     # publish 결과물
│  └─ run.sh                   # 실행 스크립트
├─ postgres/                   # Auth DB (Docker)
│  ├─ docker-compose.yml
│  └─ data/                    # PostgreSQL 볼륨 데이터
├─ gameserver/                 # Unity Dedicated Server 빌드(20개 프로세스)
│  ├─ StupidGuysServer.x86_64
│  ├─ StupidGuysServer_Data/
│  └─ run-7778.sh ~ run-7798.sh
└─ logs/
```

## 4) 매치메이킹 서버 배포 절차 (EC2)

1. **로컬에서 서버 publish**
   ```bash
   cd Server/StupidGuysServer/StupidGuysServer
   dotnet publish -c Release -o publish
   ```
2. **EC2로 업로드**
   ```bash
   scp -r publish/ ubuntu@<ElasticIP>:/opt/stupidguys/matchmaking/StupidGuysServer/
   ```
3. **EC2 실행 스크립트 예시**
   ```bash
   # /opt/stupidguys/matchmaking/run.sh
   export PORT=10000
   export GAME_SERVER_HOST=<ElasticIP>
   export GAME_SERVER_PORT=7778
   export ALLOCATION_PORT_START=7778
   export ALLOCATION_PORT_END=7798
   dotnet /opt/stupidguys/matchmaking/StupidGuysServer/StupidGuysServer.dll
   ```
4. **실행**
   ```bash
   chmod +x /opt/stupidguys/matchmaking/run.sh
   /opt/stupidguys/matchmaking/run.sh
   ```

## 5) Unity Dedicated Server 배포 절차 (EC2)

1. **Unity에서 Linux Server 빌드**
   - Build Target: Linux Server
   - `StupidGuysServer.x86_64` 및 `StupidGuysServer_Data` 생성

2. **EC2 업로드**
   ```bash
   scp -r StupidGuysServer.x86_64 StupidGuysServer_Data \
     ubuntu@<ElasticIP>:/opt/stupidguys/gameserver/
   ```

3. **EC2 실행 스크립트 예시**
   ```bash
   # /opt/stupidguys/gameserver/run.sh
   export GAME_SERVER_HOST=0.0.0.0
   export GAME_SERVER_PORT=7778
   export USE_PLAYFAB_GSDK=false
   chmod +x /opt/stupidguys/gameserver/StupidGuysServer.x86_64
   /opt/stupidguys/gameserver/StupidGuysServer.x86_64 -batchmode -nographics
   ```

4. **실행**
   ```bash
   chmod +x /opt/stupidguys/gameserver/run.sh
   /opt/stupidguys/gameserver/run.sh
   ```

5. **20개 포트 프로세스 띄우기 예시**
   ```bash
   for port in $(seq 7778 7798); do
     export GAME_SERVER_PORT=$port
     /opt/stupidguys/gameserver/StupidGuysServer.x86_64 -batchmode -nographics &
   done
   ```

## 6) 클라이언트 연결 흐름 요약

1. 클라이언트가 `MATCHMAKING_SERVER_URL`로 매치메이킹 서버에 접속
2. 매치메이킹 서버는 `GAME_SERVER_HOST`와 포트 풀(`ALLOCATION_PORT_START`~`ALLOCATION_PORT_END`)에서 할당된 포트를 전달
3. 클라이언트는 받은 IP/PORT로 게임 서버에 접속

> 매치가 종료되면 해당 로비의 포트를 다시 풀에 반환해야 합니다.  
> 매치 종료 시 서버 측에서 `CompleteMatch(lobbyId)` 호출을 추가로 해주도록 구성하세요.

## 7) 운영 팁

- 서버가 재부팅되어도 계속 실행되도록 `systemd` 서비스나 `tmux/screen` 사용 권장
- 로그는 `/opt/stupidguys/logs/`에 리다이렉션하거나 `journalctl`로 관리
- 필요한 경우 Nginx 리버스 프록시로 `:10000`을 숨길 수 있음

---

## 8) Auth DB (PostgreSQL) → EC2 Docker 이전

Auth 서버는 `DATABASE_URL` 또는 `appsettings.json`의 `Default` 연결 문자열을 사용합니다.
Render URL이 아닌 **일반 PostgreSQL 연결 문자열**을 넣으면 그대로 사용됩니다.

### 8-1) EC2 도커 PostgreSQL 구성

`/opt/stupidguys/postgres/docker-compose.yml` 예시:

```yaml
version: "3.9"
services:
  auth-postgres:
    image: postgres:16
    container_name: auth-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: stupidguys_auth
      POSTGRES_USER: stupidguys
      POSTGRES_PASSWORD: "change-me"
    ports:
      - "5432:5432"
    volumes:
      - /opt/stupidguys/postgres/data:/var/lib/postgresql/data
```

실행:
```bash
cd /opt/stupidguys/postgres
docker compose up -d
```

> 보안그룹에서 `5432/tcp`는 **필요한 서버만** 접근하도록 제한하세요.
> 같은 EC2에서 Auth 서버를 돌릴 경우, 외부 포트 오픈 없이 로컬 접속(127.0.0.1)만 허용해도 됩니다.

### 8-2) Render → EC2 데이터 마이그레이션

1. **Render DB 덤프**
   ```bash
   pg_dump "$RENDER_DATABASE_URL" --format=custom --file=auth.dump
   ```
2. **EC2로 업로드**
   ```bash
   scp auth.dump ubuntu@<ElasticIP>:/opt/stupidguys/postgres/
   ```
3. **EC2에서 복원**
   ```bash
   pg_restore \
     --dbname="postgresql://stupidguys:change-me@localhost:5432/stupidguys_auth" \
     --clean --if-exists \
     /opt/stupidguys/postgres/auth.dump
   ```

### 8-3) Auth 서버 환경 변수(연결 문자열)

EC2에서 Auth 서버가 사용할 `DATABASE_URL` 예시:

```bash
export DATABASE_URL="Host=127.0.0.1;Port=5432;Database=stupidguys_auth;Username=stupidguys;Password=change-me"
```

> 같은 EC2 내부에서만 접근하는 경우 `Host=127.0.0.1` 또는 `Host=localhost` 사용.
> 다른 서버에서 접근할 경우 `Host=<EC2_PRIVATE_IP>`를 사용하고 보안그룹을 제한하세요.

### 8-4) Auth 서버 배포/실행

1. **로컬에서 publish**
   ```bash
   cd Server/Persistence/Auth
   dotnet publish -c Release -o publish
   ```
2. **EC2로 업로드**
   ```bash
   scp -r publish/ ubuntu@<ElasticIP>:/opt/stupidguys/auth/Auth/
   ```
3. **EC2 실행 스크립트 예시**
   ```bash
   # /opt/stupidguys/auth/run.sh
   export ASPNETCORE_URLS=http://0.0.0.0:5000
   export DATABASE_URL="Host=127.0.0.1;Port=5432;Database=stupidguys_auth;Username=stupidguys;Password=change-me"
   dotnet /opt/stupidguys/auth/Auth/Auth.dll
   ```
4. **실행**
   ```bash
   chmod +x /opt/stupidguys/auth/run.sh
   /opt/stupidguys/auth/run.sh
   ```

### 8-5) 수정 포인트 요약

- **환경 변수 변경**
  - `DATABASE_URL`: Render의 `postgresql://` URL → EC2 내부 PostgreSQL 연결 문자열
  - 필요 시 `ASPNETCORE_URLS`로 Auth 서버 바인딩 포트 지정
- **인프라 변경**
  - Render DB 사용 중지 → EC2 Docker(PostgreSQL) 컨테이너 운영
  - EC2 보안그룹에서 DB 포트 접근 범위를 제한
