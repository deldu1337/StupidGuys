# StupidGuys

StupidGuys는 Fallguys를 참고하여 제작한 Unity 기반의 실시간 멀티플레이 파티 게임 프로젝트입니다.
플레이어는 로그인 후 매치메이킹을 통해 같은 방의 유저들과 매칭되고, AWS EC2에 운영 중인 전용 서버들을 거쳐 할당된 Unity Dedicated Game Server로 이동해 인게임 라운드를 플레이합니다.

---

## 프로젝트 한눈에 보기

- **장르**: 캐주얼 파티/레이스형 멀티플레이
- **핵심 경험**:
  - 로그인/회원가입
  - 매치메이킹 로비 대기
  - 전용 게임 서버로 입장 후 실시간 플레이
  - 라운드 종료 및 결과 확인
- **구성**:
  - Unity 클라이언트
  - Auth API 서버
  - SignalR 매치메이킹 서버
  - Unity Dedicated Game Server
  - TCP 채팅 서버
- **운영 환경**:
  - 서버 구성요소 전체를 AWS EC2 기반으로 운영
  - 포트 단위로 역할을 분리해 배포 및 관리
    
---

## 게임 흐름

1. **Auth 씬**에서 로그인 또는 회원가입 (EC2의 Auth API 호출)
2. **MatchMaking 씬**에서 서버 연결 및 매칭 시작
3. 매칭 완료 시 게임 서버 IP/Port를 전달받아 **InGame 씬**으로 이동
4. 라운드 진행(타이머/순위/골인 처리)
5. 결과 확인 후 다음 플로우로 전환

---

## 기술 스택

### 클라이언트
- Unity 6 (`6000.2.8f1`)
- C#
- Unity Netcode for GameObjects
- Unity Transport (UTP)
- Unity Multiplayer Roles
- SignalR .NET Client

### 서버
- **Auth 서버**: ASP.NET Core Web API (.NET 8), EF Core, PostgreSQL(Npgsql), JWT
- **매치메이킹 서버**: ASP.NET Core SignalR (.NET 8)
- **게임 서버**: Unity Linux Dedicated Server (배치 모드), PlayFab GSDK 분기 지원
- **운영 스크립트**: watchdog 기반 게임 서버 프로세스 관리

### 운영/배포
- AWS EC2 기반 운영
- Docker (PostgreSQL 배포에 활용)
- 서버 바이너리/스크립트/로그를 EC2 디렉토리에 표준화하여 관리

---

## 아키텍처 개요

```text
[Unity Client]
   ├─ Auth API (/auth, /user)
   │      └─ PostgreSQL
   └─ Matchmaking SignalR Hub (/matchmaking)
            └─ game server port allocation
                    └─ Unity Dedicated Server (UDP)
```

### 역할 분리
- **Auth API**: 계정 생성/로그인/유저 정보
- **Matchmaking**: 로비 생성/입장, 인원 집계, 타임아웃 처리, 서버 포트 할당
- **Dedicated Server**: 실제 게임 플레이 동기화/판정(타이머, 순위, 골인 처리)
- **Client**: UI/연출, 서버 통신, 씬 전환

---

## 클라이언트 디렉토리 구조

```text
StupidGuys/
├─ Unity/
│  ├─ Assets/Scripts/
│  │  ├─ OutGame/                # 로그인/회원가입 및 인증 통신
│  │  ├─ MatchMaking/MMScripts/  # SignalR 매치메이킹, 스킨 선택
│  │  ├─ InGame/                 # 인게임 네트워크/플레이 로직
│  │  ├─ Lobby/
│  │  └─ TCPSocket/              # TCP 채팅
│  ├─ Packages/
│  └─ ProjectSettings/
└─ Server/
   ├─ Persistence/Auth/          # Auth API + EF Core + JWT
   └─ StupidGuysServer/          # SignalR 매치메이킹 서버 + 운영 스크립트
```

## 서버 디렉토리 구조

```text
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
```

---

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

## 서버/인프라 운영 포인트

매치메이킹 서버는 매칭 완료 시 사용 가능한 게임 서버 포트를 할당하고, 클라이언트는 전달받은 IP/Port로 Dedicated Server에 접속합니다.

게임 서버는 크래시/종료 상황에 대비해 watchdog 스크립트로 자동 재기동되도록 하여 무중단 서버를 운영합니다.

클라이언트가 서버에 접근하기 위해 엔드포인트가 노출될 수 있는 구조이므로, 운영 전 EC2 보안 그룹 인바운드 규칙을 최소 허용 방식으로 설정하여 접근 리스크를 줄였습니다.
- 예: SSH(22)는 개인 IP만 허용

서비스 포트는 목적에 맞게 범위를 제한하고, 테스트 환경에서는 허용 대상을 좁혀 운영했습니다.
