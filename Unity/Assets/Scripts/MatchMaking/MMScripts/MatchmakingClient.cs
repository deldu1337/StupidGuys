using System;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;

public class MatchmakingClient : MonoBehaviour
{
    [SerializeField] private string serverUrl = "";

    [SerializeField] private int maxPlayers = 6;

    private HubConnection _connection;
    private MatchmakingResultData _currentMatchResult;

    private int? _currentLobbyId;

    public event Action<LobbyStatusData> OnLobbyUpdated;
    public event Action<string> OnError;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public MatchmakingResultData CurrentMatchResult => _currentMatchResult;

    private void Awake()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterServerEvents();

        _connection.Closed += OnConnectionClosed;
        _connection.Reconnecting += OnConnectionReconnecting;
        _connection.Reconnected += OnConnectionReconnected;
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
        if (_connection.State != HubConnectionState.Connected)
        {
            Debug.LogError("[SignalR] Not connected to server!");
            OnError?.Invoke("Not connected to server");
            return null;
        }

        try
        {
            Debug.Log($"[SignalR] Requesting matchmaking (maxPlayers: {maxPlayers})...");

            var result = await _connection.InvokeAsync<MatchmakingResultData>(
                "FindOrCreateLobby",
                maxPlayers
            );

            _currentMatchResult = result;

            Debug.Log($"[SignalR] Joined lobby {result.LobbyId}");
            Debug.Log($"[SignalR] Game Server: {result.GameServerIP}:{result.GameServerPort}");

            return result;
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
        if (_connection.State == HubConnectionState.Connected)
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
        try
        {
            Debug.Log($"[SignalR] Cancelling matchmaking for lobby {_currentMatchResult.LobbyId}...");

            await _connection.InvokeAsync("LeaveLobby", _currentMatchResult.LobbyId);

            _currentMatchResult = null;
            _currentLobbyId = null;

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

    private async void OnDestroy()
    {
        await DisconnectAsync();
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