using Microsoft.AspNetCore.SignalR;
using StupidGuysServer.Configuration;
using StupidGuysServer.Models;
using StupidGuysServer.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

public class MatchmakingHub : Hub
{
    private readonly LobbiesManager _lobbiesManager;
    private readonly GameServerSettings _gameServerSettings;
    private readonly MatchmakingSettings _matchmakingSettings;
    private readonly GameServerAllocator _gameServerAllocator;
    private readonly IHubContext<MatchmakingHub> _hubContext;

    public MatchmakingHub(
        LobbiesManager lobbiesManager,
        GameServerSettings gameServerSettings,
        MatchmakingSettings matchmakingSettings,
        GameServerAllocator gameServerAllocator,
        IHubContext<MatchmakingHub> hubContext)
    {
        _lobbiesManager = lobbiesManager;
        _gameServerSettings = gameServerSettings;
        _matchmakingSettings = matchmakingSettings;
        _gameServerAllocator = gameServerAllocator;
        _hubContext = hubContext;
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"[SignalR] Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] Client disconnected: {connectionId}");

        var lobby = _lobbiesManager.RemovePlayerFromAllLobbies(connectionId);
        if (lobby != null)
        {
            await Groups.RemoveFromGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));
            await NotifyLobbyUpdated(lobby);
            await CleanupLobbyIfEmptyAsync(lobby);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<MatchmakingResult> FindOrCreateLobby(int maxPlayers)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] {connectionId} requested FindOrCreateLobby (maxPlayers: {maxPlayers})");

        // ✅ 혹시 남아있던 멤버 상태를 먼저 제거
        var removedLobby = _lobbiesManager.RemovePlayerFromAllLobbies(connectionId);
        if (removedLobby != null)
        {
            await Groups.RemoveFromGroupAsync(connectionId, GetLobbyGroupName(removedLobby.Id));
            await NotifyLobbyUpdated(removedLobby);
            await CleanupLobbyIfEmptyAsync(removedLobby);
        }

        var lobby = _lobbiesManager.FindAvailableLobby();
        if (lobby == null)
        {
            lobby = _lobbiesManager.CreateLobby(maxPlayers);
            Console.WriteLine($"[SignalR] Created new lobby {lobby.Id}");
            StartAllocationTimer(lobby);
        }

        if (lobby.TryAddMember(connectionId, out _))
        {
            await Groups.AddToGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));

            Console.WriteLine($"[SignalR] {connectionId} joined lobby {lobby.Id} ({lobby.MemberCount}/{lobby.MaxPlayers})");

            await NotifyLobbyUpdated(lobby);

            if (lobby.IsFull)
            {
                await TryAllocateLobbyAsync(lobby);
            }

            return new MatchmakingResult
            {
                LobbyId = lobby.Id,
                GameServerIP = string.Empty,
                GameServerPort = 0,
                Success = true
            };
        }

        Console.WriteLine($"[SignalR] Failed to add {connectionId} to lobby {lobby.Id}");
        throw new HubException("Failed to join lobby");
    }

    public LobbyStatus GetLobbyStatus(int lobbyId)
    {
        var lobby = _lobbiesManager.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException($"Lobby {lobbyId} not found");

        return new LobbyStatus
        {
            Id = lobby.Id,
            CurrentPlayers = lobby.MemberCount,
            MaxPlayers = lobby.MaxPlayers,
            IsFull = lobby.IsFull
        };
    }

    public async Task LeaveLobby(int lobbyId)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] {connectionId} requested LeaveLobby ({lobbyId})");

        var lobby = _lobbiesManager.GetLobby(lobbyId);
        if (lobby == null)
            return;

        if (lobby.TryRemoveMember(connectionId, out _))
        {
            await Groups.RemoveFromGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));
            await NotifyLobbyUpdated(lobby);
            await CleanupLobbyIfEmptyAsync(lobby);
        }
    }

    public async Task CompleteMatch(int lobbyId)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] Completing match for lobby {lobbyId} by {connectionId}");

        var lobby = _lobbiesManager.GetLobby(lobbyId);
        if (lobby == null)
            return;

        // ✅ 완료 보고한 클라는 즉시 멤버 제거(재매칭 가능)
        lobby.TryRemoveMember(connectionId, out _);
        await Groups.RemoveFromGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));

        await NotifyLobbyUpdated(lobby);
        await CleanupLobbyIfEmptyAsync(lobby);
    }

    private async Task NotifyLobbyUpdated(Lobby lobby)
    {
        var status = new LobbyStatus
        {
            Id = lobby.Id,
            CurrentPlayers = lobby.MemberCount,
            MaxPlayers = lobby.MaxPlayers,
            IsFull = lobby.IsFull
        };

        await Clients.Group(GetLobbyGroupName(lobby.Id))
            .SendAsync("LobbyUpdated", status);

        Console.WriteLine($"[SignalR] Notified lobby {lobby.Id} update: {status.CurrentPlayers}/{status.MaxPlayers}");
    }

    private void StartAllocationTimer(Lobby lobby)
    {
        var cts = new CancellationTokenSource();
        if (!lobby.TryStartAllocationTimer(cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_matchmakingSettings.TimeoutSeconds), cts.Token);

                while (!cts.Token.IsCancellationRequested)
                {
                    bool allocated = await TryAllocateLobbyAsync(lobby);
                    if (allocated)
                        return;

                    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        });
    }

    private async Task<bool> TryAllocateLobbyAsync(Lobby lobby)
    {
        if (lobby.IsMatchFinalized || lobby.MemberCount == 0)
            return false;

        var elapsed = DateTime.UtcNow - lobby.CreatedAtUtc;
        if (!lobby.IsFull && elapsed.TotalSeconds < _matchmakingSettings.TimeoutSeconds)
            return false;

        if (!_gameServerAllocator.TryAllocate(out var port))
            return false;

        if (!lobby.TryFinalizeMatch(_gameServerSettings.Host, port))
        {
            _gameServerAllocator.Release(port);
            return false;
        }

        lobby.AllocationCancellation?.Cancel();

        var result = new MatchmakingResult
        {
            LobbyId = lobby.Id,
            GameServerIP = lobby.GameServerIP,
            GameServerPort = lobby.GameServerPort,
            Success = true
        };

        await _hubContext.Clients.Group(GetLobbyGroupName(lobby.Id))
            .SendAsync("MatchAllocated", result);

        Console.WriteLine($"[SignalR] Allocated game server: {lobby.GameServerIP}:{lobby.GameServerPort} for lobby {lobby.Id}");
        return true;
    }

    // ✅ Release/Remove는 여기서만, 딱 1번만
    private async Task CleanupLobbyIfEmptyAsync(Lobby lobby)
    {
        if (lobby.MemberCount != 0)
            return;

        if (!lobby.TryBeginCleanup())
            return;

        lobby.AllocationCancellation?.Cancel();

        if (lobby.IsGameServerAllocated)
        {
            _gameServerAllocator.Release(lobby.GameServerPort);
            Console.WriteLine($"[SignalR] Released port {lobby.GameServerPort} for lobby {lobby.Id}");
        }

        _lobbiesManager.RemoveLobby(lobby.Id);
        Console.WriteLine($"[SignalR] Removed lobby {lobby.Id}");

        await Task.CompletedTask;
    }

    private string GetLobbyGroupName(int lobbyId) => $"lobby_{lobbyId}";
}

public class LobbyStatus
{
    public int Id { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsFull { get; set; }
}

public class MatchmakingResult
{
    public int LobbyId { get; set; }
    public string? GameServerIP { get; set; }
    public int GameServerPort { get; set; }
    public bool Success { get; set; }
}
