using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;

public class MatchmakingClient : MonoBehaviour
{
    private const string DefaultServerUrl = "http://3.37.215.9:10000/matchmaking";

    [SerializeField] private string serverUrl = "";
    [SerializeField] private int maxPlayers = 6;

    private HubConnection _connection;
    private MatchmakingResultData _currentMatchResult;

    public static MatchmakingClient Instance { get; private set; }

    public event Action<LobbyStatusData> OnLobbyUpdated;
    public event Action<MatchmakingResultData> OnMatchAllocated;
    public event Action<string> OnError;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public MatchmakingResultData CurrentMatchResult => _currentMatchResult;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureConnectionBuilt();
    }

    private string ResolveServerUrl()
    {
        if (!string.IsNullOrWhiteSpace(serverUrl))
            return serverUrl;

        return DefaultServerUrl;
    }

    /// <summary>
    /// ✅ _connection이 null(또는 깨진 상태)일 수 있으니, 모든 API 앞에서 보장.
    /// 씬 왕복/도메인 리로드/오브젝트 재생성 꼬임에서도 복구됨.
    /// </summary>
    private void EnsureConnectionBuilt()
    {
        if (_connection != null)
            return;

        serverUrl = ResolveServerUrl();

        _connection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterServerEvents();

        _connection.Closed += OnConnectionClosed;
        _connection.Reconnecting += OnConnectionReconnecting;
        _connection.Reconnected += OnConnectionReconnected;

        Debug.Log("[SignalR] HubConnection built");
    }

    private void RegisterServerEvents()
    {
        _connection.On<LobbyStatusData>("LobbyUpdated", status =>
        {
            Debug.Log($"[SignalR] Lobby updated: {status.CurrentPlayers}/{status.MaxPlayers}");
            UnityMainThreadDispatcher.Enqueue(() => OnLobbyUpdated?.Invoke(status));
        });

        _connection.On<MatchmakingResultData>("MatchAllocated", result =>
        {
            Debug.Log($"[SignalR] Match allocated: {result.GameServerIP}:{result.GameServerPort} (lobby {result.LobbyId})");
            _currentMatchResult = result;
            UnityMainThreadDispatcher.Enqueue(() => OnMatchAllocated?.Invoke(result));
        });

        _connection.On<string>("MatchmakingError", message =>
        {
            Debug.LogError($"[SignalR] Matchmaking error: {message}");
            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke(message));
        });
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            EnsureConnectionBuilt(); // ✅ 여기서 null 방지

            Debug.Log($"[SignalR] ConnectAsync called, state={_connection.State}");

            if (_connection.State == HubConnectionState.Connected)
                return true;

            // ✅ AutomaticReconnect와 충돌 방지: Connecting/Reconnecting이면 Stop/Start 하지 말고 기다림
            if (_connection.State == HubConnectionState.Connecting ||
                _connection.State == HubConnectionState.Reconnecting)
            {
                Debug.Log($"[SignalR] Waiting while {_connection.State}...");
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(100);
                    if (_connection.State == HubConnectionState.Connected)
                        return true;
                }
                return _connection.State == HubConnectionState.Connected;
            }

            // Disconnected 상태만 Start
            Debug.Log($"[SignalR] Starting connection to {serverUrl}...");
            await _connection.StartAsync();

            Debug.Log("[SignalR] Connected!");
            UnityMainThreadDispatcher.Enqueue(() => OnConnected?.Invoke());
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Connection failed: {ex}");
            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Connection failed: {ex.Message}"));
            return false;
        }
    }

    public async Task<MatchmakingResultData> StartMatchmakingAsync()
    {
        try
        {
            EnsureConnectionBuilt(); // ✅ 여기서 null 방지
            Debug.Log($"[SignalR] StartMatchmakingAsync begin, state={_connection.State}");

            bool connected = await ConnectAsync();
            if (!connected)
            {
                UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("Not connected to server"));
                return null;
            }

            _currentMatchResult = null;

            Debug.Log($"[SignalR] Invoking FindOrCreateLobby (maxPlayers={maxPlayers}) state={_connection.State}");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var result = await _connection.InvokeAsync<MatchmakingResultData>(
                "FindOrCreateLobby",
                maxPlayers,
                cts.Token
            );

            _currentMatchResult = result;
            Debug.Log($"[SignalR] Joined lobby {result.LobbyId}");
            return result;
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[SignalR] Matchmaking request timed out");
            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("Matchmaking request timed out"));
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Matchmaking failed: {ex}");
            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Matchmaking failed: {ex.Message}"));
            return null;
        }
    }

    public async Task<bool> CancelMatchmakingAsync()
    {
        EnsureConnectionBuilt();

        if (_currentMatchResult == null || _currentMatchResult.LobbyId <= 0)
        {
            Debug.Log("[SignalR] No active lobby to cancel");
            return true;
        }

        bool connected = await ConnectAsync();
        if (!connected) return false;

        try
        {
            Debug.Log($"[SignalR] Cancelling matchmaking for lobby {_currentMatchResult.LobbyId}...");
            await _connection.InvokeAsync("LeaveLobby", _currentMatchResult.LobbyId);
            _currentMatchResult = null;
            Debug.Log("[SignalR] Matchmaking cancelled successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Failed to cancel matchmaking: {ex}");
            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Failed to cancel: {ex.Message}"));
            return false;
        }
    }

    public async Task<bool> CompleteMatchAsync(int lobbyId)
    {
        EnsureConnectionBuilt();

        if (lobbyId <= 0)
        {
            Debug.LogWarning("[SignalR] Invalid lobby id for completion");
            return false;
        }

        bool connected = await ConnectAsync();
        if (!connected) return false;

        try
        {
            Debug.Log($"[SignalR] Completing match for lobby {lobbyId}...");
            await _connection.InvokeAsync("CompleteMatch", lobbyId);
            _currentMatchResult = null;
            Debug.Log("[SignalR] Match completion reported");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Failed to complete match: {ex}");
            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Failed to complete match: {ex.Message}"));
            return false;
        }
    }

    private Task OnConnectionClosed(Exception exception)
    {
        Debug.LogWarning($"[SignalR] Connection closed: {exception?.Message ?? "Unknown"}");
        UnityMainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke());
        return Task.CompletedTask;
    }

    private Task OnConnectionReconnecting(Exception exception)
    {
        Debug.LogWarning($"[SignalR] Reconnecting... {exception?.Message ?? ""}");
        return Task.CompletedTask;
    }

    private Task OnConnectionReconnected(string connectionId)
    {
        Debug.Log($"[SignalR] Reconnected! ConnectionId: {connectionId}");
        UnityMainThreadDispatcher.Enqueue(() => OnConnected?.Invoke());
        return Task.CompletedTask;
    }

    private async void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_connection != null)
        {
            try { await _connection.StopAsync(); } catch { }
            try { await _connection.DisposeAsync(); } catch { }
            _connection = null;
        }
    }

    private async void OnApplicationQuit()
    {
        if (_connection != null)
        {
            try { await _connection.StopAsync(); } catch { }
            try { await _connection.DisposeAsync(); } catch { }
            _connection = null;
        }
    }
}

[Serializable]
public class MatchmakingResultData
{
    public int LobbyId { get; set; }
    public string GameServerIP { get; set; }
    public int GameServerPort { get; set; }
    public bool Success { get; set; }
}

[Serializable]
public class LobbyStatusData
{
    public int Id { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsFull { get; set; }
}
