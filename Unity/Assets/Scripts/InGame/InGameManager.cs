using UnityEngine;
using Unity.Netcode;
using Unity.Multiplayer;
using System.Collections;
using System;
using System.Collections.Generic;
using Unity.Collections;

public enum InGameState
{
    None,
    WaitUntilAllClientsAreConntected,
    StartContent,
    WaitUntilContentFinished,
}

public struct MatchInfo : INetworkSerializable
{
    public int clientCount;
    public FixedString64Bytes title;

    /// <summary>
    /// 매치 정보를 네트워크로 동기화하기 위해 직렬화와 역직렬화를 수행한다
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientCount);
        serializer.SerializeValue(ref title);
    }
}

public class InGameManager : NetworkBehaviour
{
    public static InGameManager singleton;

    // 현재 인게임 진행 상태를 나타낸다
    // 서버만 값을 변경할 수 있고 모든 클라이언트가 읽을 수 있다
    public NetworkVariable<InGameState> state = new NetworkVariable<InGameState>(
        value: InGameState.None,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // 매치의 기본 정보를 네트워크로 공유한다
    // 서버만 값을 변경할 수 있고 모든 클라이언트가 읽을 수 있다
    public NetworkVariable<MatchInfo> matchInfo = new NetworkVariable<MatchInfo>(
        value: default,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // 현재 접속해 있는 클라이언트들의 아이디 목록을 관리한다
    // 서버가 작성하고 모든 클라이언트가 읽는다
    public NetworkList<ulong> connectedClientIds = new NetworkList<ulong>(
        values: null,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // 서버가 관리하는 골인 카운트이며 UI의 현재 성공 인원 표시로 사용한다
    public NetworkVariable<int> CurRank = new NetworkVariable<int>(
        value: 0,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // 서버에서만 사용하며 골인 처리를 중복으로 올리지 않기 위한 집합이다
    private readonly HashSet<ulong> _goalClients = new HashSet<ulong>();

    // 시작 전 입력 잠금을 위한 플래그이며 서버가 제어한다
    public NetworkVariable<bool> IsFrozen = new NetworkVariable<bool>(
        value: true,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // 라운드 제한 시간이며 서버 시간 기준으로 남은 시간을 계산할 때 사용한다
    [SerializeField] private float roundDurationSeconds = 180f;

    // 라운드 시작 시각을 서버 시간으로 고정해 전 클라이언트가 동일한 남은 시간을 계산할 수 있게 한다
    public NetworkVariable<double> RoundStartServerTime = new NetworkVariable<double>(
        value: -1,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // 카운트다운 길이이며 4면 3 2 1 0 순으로 내려간다
    [SerializeField] private int startCountdownTime = 4;

    // 클라이언트가 카운트다운 UI를 표시할 수 있게 하는 이벤트이다
    public static event Action<int> OnCountdownTick;

    private Coroutine _countdownRoutine;

    // 서버에서만 사용하며 각 클라이언트의 스폰 배치 완료 여부를 확인한다
    private readonly HashSet<ulong> _spawnReadyClients = new HashSet<ulong>();

    /// <summary>
    /// 싱글톤 참조를 초기화한다
    /// </summary>
    private void Awake()
    {
        singleton = this;
    }

    /// <summary>
    /// 네트워크 스폰 시 서버에서 매치 정보를 초기화하고
    /// 클라이언트 접속과 해제 콜백 및 상태 변경 콜백을 등록한다
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            matchInfo.Value = new MatchInfo
            {
                clientCount = 2,
                title = "Noobs only"
            };

            IsFrozen.Value = true;
            RoundStartServerTime.Value = -1;

            state.Value = InGameState.WaitUntilAllClientsAreConntected;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            state.OnValueChanged += OnStateChanged_ServerOnly;
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시 서버에서 등록했던 콜백을 해제한다
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            state.OnValueChanged -= OnStateChanged_ServerOnly;
        }
    }

    /// <summary>
    /// 서버에서 클라이언트가 연결되었을 때 아이디 목록에 추가하고
    /// 모든 클라이언트가 연결되고 스폰 배치가 끝났는지 확인한다
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        connectedClientIds.Add(clientId);
        TryStartWhenAllConnectedAndSpawned();
    }

    /// <summary>
    /// 서버에서 클라이언트가 연결 해제되었을 때 아이디 목록에서 제거하고
    /// 스폰 준비 집합에서도 제거한다
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        connectedClientIds.Remove(clientId);
        _spawnReadyClients.Remove(clientId);

        if (connectedClientIds.Count == 0)
        {
            Server_ResetForReuse();
        }
    }

    /// <summary>
    /// PlayerSpawnManager가 서버에서 특정 클라이언트의 스폰 배치가 완료되었음을 알릴 때 호출한다
    /// 서버는 준비 집합에 추가하고 시작 조건을 다시 확인한다
    /// </summary>
    public void Server_NotifyClientSpawnReady(ulong clientId)
    {
        if (!IsServer) return;

        _spawnReadyClients.Add(clientId);
        TryStartWhenAllConnectedAndSpawned();
    }

    /// <summary>
    /// 서버에서 기대 인원만큼 클라이언트가 연결되고 스폰 배치까지 완료되면 콘텐츠 시작 상태로 전환한다
    /// </summary>
    private void TryStartWhenAllConnectedAndSpawned()
    {
        if (!IsServer) return;
        if (state.Value != InGameState.WaitUntilAllClientsAreConntected) return;

        int expected = matchInfo.Value.clientCount;

        bool allConnected = connectedClientIds.Count >= expected;
        bool allSpawned = _spawnReadyClients.Count >= expected;

        if (allConnected && allSpawned)
        {
            state.Value = InGameState.StartContent;
        }
    }

    /// <summary>
    /// 서버에서만 상태 변경을 감지해 콘텐츠 시작 상태로 바뀌면 카운트다운 후 라운드를 시작한다
    /// </summary>
    private void OnStateChanged_ServerOnly(InGameState prev, InGameState next)
    {
        if (!IsServer) return;

        if (next == InGameState.StartContent)
        {
            if (_countdownRoutine != null)
                StopCoroutine(_countdownRoutine);

            _countdownRoutine = StartCoroutine(Server_CountdownThenStartRound());
        }
    }

    /// <summary>
    /// 서버에서 카운트다운을 진행한 뒤 라운드 시작 시각을 고정하고
    /// 입력 잠금을 해제하며 진행 상태를 콘텐츠 진행 중 상태로 전환한다
    /// </summary>
    private IEnumerator Server_CountdownThenStartRound()
    {
        IsFrozen.Value = true;
        RoundStartServerTime.Value = -1;

        Server_ResetRank();

        int t = startCountdownTime;

        while (t > 0)
        {
            CountdownTickClientRpc(t);
            yield return new WaitForSeconds(1f);
            t--;
        }

        CountdownTickClientRpc(0);

        RoundStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;

        IsFrozen.Value = false;
        state.Value = InGameState.WaitUntilContentFinished;

        _countdownRoutine = null;
    }

    /// <summary>
    /// 서버에서 계산한 카운트다운 값을 모든 클라이언트에게 전달해 UI가 표시되도록 한다
    /// </summary>
    [ClientRpc]
    private void CountdownTickClientRpc(int t)
    {
        OnCountdownTick?.Invoke(t);
    }

    /// <summary>
    /// 서버에서 관리하는 골인 카운트를 초기화하고 중복 방지 집합도 비운다
    /// </summary>
    public void Server_ResetRank()
    {
        if (!IsServer) return;
        CurRank.Value = 0;
        _goalClients.Clear();
    }

    /// <summary>
    /// 모든 클라이언트가 나간 후 서버를 재사용할 수 있도록 상태를 초기화한다.
    /// </summary>
    private void Server_ResetForReuse()
    {
        if (!IsServer) return;

        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        connectedClientIds.Clear();
        _spawnReadyClients.Clear();
        Server_ResetRank();

        IsFrozen.Value = true;
        RoundStartServerTime.Value = -1;
        state.Value = InGameState.WaitUntilAllClientsAreConntected;
    }

    /// <summary>
    /// 서버에서 특정 클라이언트의 골인을 등록하고 중복이면 무시한다
    /// 정상 등록이면 골인 카운트를 1 증가시킨다
    /// </summary>
    public void Server_RegisterGoal(ulong clientId)
    {
        if (!IsServer) return;

        if (!_goalClients.Add(clientId))
            return;

        CurRank.Value = CurRank.Value + 1;
    }

    /// <summary>
    /// 라운드 시작 시각과 현재 서버 시간을 이용해 남은 시간을 계산해 반환한다
    /// 라운드가 아직 시작되지 않았으면 기본 라운드 시간을 반환한다
    /// </summary>
    public float GetRemainingTimeSeconds()
    {
        if (RoundStartServerTime.Value < 0) return roundDurationSeconds;

        double now = NetworkManager.Singleton.ServerTime.Time;
        double elapsed = now - RoundStartServerTime.Value;
        float remain = roundDurationSeconds - (float)elapsed;
        return Mathf.Max(0f, remain);
    }
}
