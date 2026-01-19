using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : NetworkBehaviour
{
    [Header("비어있으면 자식 오브젝트 중 SpawnPoint로 시작하는 이름을 자동 수집")]
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, int> assigned = new();
    private readonly HashSet<int> used = new();

    // 현재 접속자 수를 네트워크로 공유한다
    // 이 값은 UI의 HeadCount 분모로 사용한다
    public NetworkVariable<int> ConnectedPlayerCount =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// 스폰 포인트 배열이 비어있으면 자식 오브젝트에서 자동으로 스폰 포인트를 수집해 초기화한다
    /// </summary>
    private void Awake()
    {
        CollectSpawnPointsIfNeeded();
    }

    /// <summary>
    /// 네트워크 스폰 시점에 스폰 포인트를 준비하고
    /// 접속자 수 변경 이벤트를 구독하며
    /// 서버라면 접속 콜백을 등록하고 각 클라이언트를 스폰 포인트로 배치한다
    /// </summary>
    public override void OnNetworkSpawn()
    {
        CollectSpawnPointsIfNeeded();

        ConnectedPlayerCount.OnValueChanged += OnConnectedPlayerCountChanged;
        OnConnectedPlayerCountChanged(0, ConnectedPlayerCount.Value);

        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        UpdateConnectedPlayerCount();

        foreach (var kv in NetworkManager.ConnectedClients)
        {
            StartCoroutine(PlaceWhenPlayerReady(kv.Key));
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시점에 이벤트 구독을 해제하고
    /// 서버라면 접속 콜백도 해제한다
    /// </summary>
    public override void OnNetworkDespawn()
    {
        ConnectedPlayerCount.OnValueChanged -= OnConnectedPlayerCountChanged;

        if (!IsServer) return;

        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    /// <summary>
    /// 접속자 수가 바뀔 때 UI에 분모 값을 전달하여 HeadCount 텍스트를 갱신한다
    /// </summary>
    private void OnConnectedPlayerCountChanged(int prev, int next)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.SetHeadCountTotal(next);
    }

    /// <summary>
    /// 서버에서 현재 접속 중인 클라이언트 수를 계산하여 네트워크 변수에 반영한다
    /// </summary>
    private void UpdateConnectedPlayerCount()
    {
        if (!IsServer || NetworkManager == null) return;

        int count = NetworkManager.ConnectedClientsIds.Count;
        ConnectedPlayerCount.Value = count;
    }

    /// <summary>
    /// spawnPoints가 비어있을 경우 자식 오브젝트 중 이름이 SpawnPoint로 시작하는 트랜스폼을 수집하고
    /// 이름 끝에 붙은 숫자를 기준으로 정렬하여 배열로 만든다
    /// </summary>
    private void CollectSpawnPointsIfNeeded()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return;

        var list = new List<Transform>();

        foreach (Transform child in transform)
        {
            if (child == transform) continue;

            if (child.name.StartsWith("SpawnPoint", StringComparison.OrdinalIgnoreCase))
                list.Add(child);
        }

        spawnPoints = list
            .OrderBy(t => ExtractTrailingNumber(t.name))
            .ToArray();
    }

    /// <summary>
    /// 문자열 끝에 붙은 숫자를 추출해 반환한다
    /// 예를 들어 SpawnPoint10 이면 10을 반환한다
    /// 숫자가 없으면 매우 큰 값을 반환하여 정렬에서 뒤로 밀리게 한다
    /// </summary>
    private int ExtractTrailingNumber(string s)
    {
        int i = s.Length - 1;
        while (i >= 0 && char.IsDigit(s[i])) i--;
        if (i == s.Length - 1) return int.MaxValue;

        string num = s.Substring(i + 1);
        return int.TryParse(num, out int v) ? v : int.MaxValue;
    }

    /// <summary>
    /// 클라이언트가 접속했을 때 서버에서 접속자 수를 갱신하고
    /// 해당 클라이언트의 플레이어 오브젝트가 준비되면 스폰 포인트로 배치한다
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        UpdateConnectedPlayerCount();
        StartCoroutine(PlaceWhenPlayerReady(clientId));
    }

    /// <summary>
    /// 해당 클라이언트의 플레이어 네트워크 오브젝트가 생성될 때까지 기다린 뒤
    /// 스폰 포인트를 할당해 서버 위치를 갱신하고
    /// 대상 클라이언트에게만 로컬 텔레포트를 수행하도록 ClientRpc를 보낸다
    /// 이후 스폰 완료를 InGameManager에 알린다
    /// </summary>
    private IEnumerator PlaceWhenPlayerReady(ulong clientId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            yield break;

        NetworkObject playerNo = null;

        while (playerNo == null && NetworkManager != null && NetworkManager.IsListening)
        {
            playerNo = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            yield return null;
        }

        if (playerNo == null)
            yield break;

        int spawnIndex = AllocatePoint(clientId);
        var sp = spawnPoints[spawnIndex];

        playerNo.transform.SetPositionAndRotation(sp.position, sp.rotation);

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };

        TeleportLocalPlayerClientRpc(sp.position, sp.rotation, rpcParams);

        if (InGameManager.singleton != null)
            InGameManager.singleton.Server_NotifyClientSpawnReady(clientId);
    }

    /// <summary>
    /// 특정 클라이언트에게 스폰 포인트 인덱스를 할당한다
    /// 우선 clientId 기반 선호 인덱스를 선택하고
    /// 이미 사용 중이면 비어있는 첫 인덱스를 선택한다
    /// 모두 사용 중이면 선호 인덱스를 그대로 사용한다
    /// </summary>
    private int AllocatePoint(ulong clientId)
    {
        if (assigned.TryGetValue(clientId, out int already))
            return already;

        int count = spawnPoints.Length;
        int preferred = (int)(clientId % (ulong)count);

        if (!used.Contains(preferred))
        {
            used.Add(preferred);
            assigned[clientId] = preferred;
            return preferred;
        }

        for (int i = 0; i < count; i++)
        {
            if (used.Contains(i)) continue;
            used.Add(i);
            assigned[clientId] = i;
            return i;
        }

        assigned[clientId] = preferred;
        return preferred;
    }

    /// <summary>
    /// 클라이언트가 연결 해제되면 할당된 스폰 포인트를 반납하고
    /// 서버에서 접속자 수를 갱신한다
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (assigned.TryGetValue(clientId, out int idx))
        {
            assigned.Remove(clientId);
            used.Remove(idx);
        }

        UpdateConnectedPlayerCount();
    }

    /// <summary>
    /// 대상 클라이언트의 로컬 플레이어를 지정 위치와 회전으로 이동시킨다
    /// 리지드바디가 있으면 속도를 초기화한 뒤 위치 회전을 적용한다
    /// </summary>
    [ClientRpc]
    private void TeleportLocalPlayerClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        var localPlayer = nm.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return;

        var rb = localPlayer.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }
        else
        {
            localPlayer.transform.SetPositionAndRotation(pos, rot);
        }
    }
}