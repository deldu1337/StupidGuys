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

        serverUrl = ResolveServerUrl();
        _connection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterServerEvents();

        _connection.Closed += OnConnectionClosed;
        _connection.Reconnecting += OnConnectionReconnecting;
        _connection.Reconnected += OnConnectionReconnected;
    }

    private string ResolveServerUrl()
    {
        if (!string.IsNullOrWhiteSpace(serverUrl))
        {
            return serverUrl;
        }

        return DefaultServerUrl;
    }

    private void RegisterServerEvents()
    {
        _connection.On<LobbyStatusData>("LobbyUpdated", (status) =>
        {
            Debug.Log($"[SignalR] Lobby updated: {status.CurrentPlayers}/{status.MaxPlayers}");

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                OnLobbyUpdated?.Invoke(status);
            });
        });

        _connection.On<MatchmakingResultData>("MatchAllocated", (result) =>
        {
            Debug.Log($"[SignalR] Match allocated: {result.GameServerIP}:{result.GameServerPort} (lobby {result.LobbyId})");
            _currentMatchResult = result;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                OnMatchAllocated?.Invoke(result);
            });
        });

        _connection.On<string>("MatchmakingError", (message) =>
        {
            Debug.LogError($"[SignalR] Matchmaking error: {message}");

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                OnError?.Invoke(message);
            });
        });
    }

    public async Task<bool> ConnectAsync()
    {
        if (_connection.State == HubConnectionState.Connected)
        {
            Debug.Log("[SignalR] Already connected");
            return true;
        }

        try
        {
            if (_connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting or HubConnectionState.Disconnecting)
            {
                await _connection.StopAsync();
            }

            Debug.Log($"[SignalR] Connecting to {serverUrl}...");
            await _connection.StartAsync();

            Debug.Log("[SignalR] Connected!");
            OnConnected?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Connection failed: {ex.Message}");
            OnError?.Invoke($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task<MatchmakingResultData> StartMatchmakingAsync()
    {
        bool connected = await ConnectAsync();
        if (!connected)
        {
            OnError?.Invoke("Not connected to server");
            return null;
        }

        try
        {
            _currentMatchResult = null;
            Debug.Log($"[SignalR] Requesting matchmaking (maxPlayers: {maxPlayers})...");

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
            Debug.LogWarning("[SignalR] Matchmaking request timed out. Reconnecting and retrying once...");

            await DisconnectAsync();
            bool reconnected = await ConnectAsync();
            if (!reconnected)
            {
                OnError?.Invoke("Matchmaking timeout and reconnect failed");
                return null;
            }

            try
            {
                using var retryCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var retryResult = await _connection.InvokeAsync<MatchmakingResultData>(
                    "FindOrCreateLobby",
                    maxPlayers,
                    retryCts.Token
                );

                _currentMatchResult = retryResult;
                Debug.Log($"[SignalR] Joined lobby {retryResult.LobbyId} (retry)");
                return retryResult;
            }
            catch (Exception retryEx)
            {
                Debug.LogError($"[SignalR] Matchmaking retry failed: {retryEx.Message}");
                OnError?.Invoke($"Matchmaking retry failed: {retryEx.Message}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Matchmaking failed: {ex.Message}");
            OnError?.Invoke($"Matchmaking failed: {ex.Message}");
            return null;
        }
    }

    public async Task<LobbyStatusData> GetLobbyStatusAsync(int lobbyId)
    {
        if (_connection.State != HubConnectionState.Connected)
        {
            Debug.LogError("[SignalR] Not connected!");
            return null;
        }

        try
        {
            var status = await _connection.InvokeAsync<LobbyStatusData>("GetLobbyStatus", lobbyId);
            return status;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalR] Failed to get lobby status: {ex.Message}");
            return null;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection.State != HubConnectionState.Disconnected)
        {
            Debug.Log("[SignalR] Disconnecting...");
            await _connection.StopAsync();
        }
    }

    private Task OnConnectionClosed(Exception exception)
    {
        Debug.LogWarning($"[SignalR] Connection closed: {exception?.Message ?? "Unknown"}");

        UnityMainThreadDispatcher.Enqueue(() => { OnDisconnected?.Invoke(); });

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

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            OnConnected?.Invoke();
        });

        return Task.CompletedTask;
    }

    public async Task<bool> CancelMatchmakingAsync()
    {
        if (_currentMatchResult == null || _currentMatchResult.LobbyId <= 0)
        {
            Debug.Log("[SignalR] No active lobby to cancel");
            return true;
        }

        bool connected = await ConnectAsync();
        if (!connected)
        {
            return false;
        }

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
            Debug.LogError($"[SignalR] Failed to cancel matchmaking: {ex.Message}");
            OnError?.Invoke($"Failed to cancel: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CompleteMatchAsync(int lobbyId)
    {
        if (lobbyId <= 0)
        {
            Debug.LogWarning("[SignalR] Invalid lobby id for completion");
            return false;
        }

        bool connected = await ConnectAsync();
        if (!connected)
        {
            return false;
        }

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
            Debug.LogError($"[SignalR] Failed to complete match: {ex.Message}");
            OnError?.Invoke($"Failed to complete match: {ex.Message}");
            return false;
        }
    }

    private async void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            await DisconnectAsync();
        }
    }

    private async void OnApplicationQuit()
    {
        await DisconnectAsync();
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
