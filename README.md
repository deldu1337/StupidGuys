# StupidGuys

StupidGuys는 Unity 기반의 실시간 멀티플레이 파티 게임 프로젝트입니다.  
플레이어는 로그인 후 매치메이킹을 통해 같은 방의 유저들과 매칭되고, 할당된 게임 서버로 이동해 인게임 라운드를 플레이합니다.

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
  - (보조) TCP 채팅 서버

---

## 게임 흐름

1. **Auth 씬**에서 로그인 또는 회원가입
2. **MatchMaking 씬**에서 서버 연결 및 매칭 시작
3. 매칭 완료 시 게임 서버 IP/Port를 전달받아 **InGame 씬**으로 이동
4. 라운드 진행(타이머/순위/골인 처리)
5. 결과 확인 후 다음 플로우로 전환

---

## 기술 스택

### 클라이언트 (Unity)
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
- Docker (PostgreSQL 배포에 활용)
- AWS EC2 기반 운영 구조

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
- **Dedicated Server**: 실제 게임 플레이 동기화/판정
- **Client**: UI/연출 + 서버 통신 + 씬 전환

---

## 디렉토리 구조

```text
StupidGuys/
├─ Unity/
│  ├─ Assets/Scripts/
│  │  ├─ OutGame/                # 로그인/회원가입 및 인증 통신
│  │  ├─ MatchMaking/MMScripts/  # SignalR 매치메이킹, 스킨 선택
│  │  ├─ InGame/                 # 인게임 네트워크/플레이 로직
│  │  ├─ Lobby/
│  │  └─ TCPSocket/              # TCP 채팅 실험/보조 코드
│  ├─ Packages/
│  └─ ProjectSettings/
└─ Server/
   ├─ Persistence/Auth/          # Auth API + EF Core + JWT
   └─ StupidGuysServer/          # SignalR 매치메이킹 서버 + 운영 스크립트
```

---

## 서버/인프라 운영 포인트 (요약)

운영 환경은 EC2 기준으로 **Auth(5000), Matchmaking(10000), TCPChat(7777), GameServer(UDP 7778 중심)** 포트를 사용합니다.  
매치메이킹 서버는 게임 서버 포트를 할당하고, 실제 게임 서버 프로세스는 watchdog 스크립트로 기동/재시작 관리하는 구조입니다.
