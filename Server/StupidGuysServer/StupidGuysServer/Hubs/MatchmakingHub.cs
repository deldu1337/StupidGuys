using Microsoft.AspNetCore.SignalR;
using StupidGuysServer.Configuration;
using StupidGuysServer.Models;
using StupidGuysServer.Services;
using System;
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
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] Client connected: {connectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] Client disconnected: {connectionId}");

        var lobby = _lobbiesManager.RemovePlayerFromAllLobbies(connectionId);

        if (lobby != null)
        {
            Console.WriteLine($"[SignalR] Removed {connectionId} from lobby {lobby.Id}");

            if (lobby.MemberCount == 0)
            {
                lobby.AllocationCancellation?.Cancel();

                if (lobby.IsGameServerAllocated)
                {
                    _gameServerAllocator.Release(lobby.GameServerPort);
                }
            }

            await NotifyLobbyUpdated(lobby);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<MatchmakingResult> FindOrCreateLobby(int maxPlayers)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] {connectionId} requested FindOrCreateLobby (maxPlayers: {maxPlayers})");

        var lobby = _lobbiesManager.FindAvailableLobby();

        if (lobby == null)
        {
            lobby = _lobbiesManager.CreateLobby(maxPlayers);
            Console.WriteLine($"[SignalR] Created new lobby {lobby.Id}");

            StartAllocationTimer(lobby);
        }

        if (lobby.TryAddMember(connectionId, out int remainMemberCount))
        {
            await Groups.AddToGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));

            Console.WriteLine($"[SignalR] {connectionId} joined lobby {lobby.Id} ({lobby.MemberCount}/{maxPlayers})");

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
        else
        {
            Console.WriteLine($"[SignalR] Failed to add {connectionId} to lobby {lobby.Id}");
            throw new HubException("Failed to join lobby");
        }
    }

    public LobbyStatus GetLobbyStatus(int lobbyId)
    {
        var lobby = _lobbiesManager.GetLobby(lobbyId);

        if (lobby == null)
        {
            throw new HubException($"Lobby {lobbyId} not found");
        }

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
        {
            return;
        }

        if (lobby.IsMatchFinalized)
        {
            return;
        }

        if (lobby.TryRemoveMember(connectionId, out int remainCount))
        {
            await Groups.RemoveFromGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));

            if (remainCount == 0)
            {
                lobby.AllocationCancellation?.Cancel();

                if (lobby.IsGameServerAllocated)
                {
                    _gameServerAllocator.Release(lobby.GameServerPort);
                }

                _lobbiesManager.RemoveLobby(lobby.Id);
            }

            await NotifyLobbyUpdated(lobby);
        }
    }

    public Task CompleteMatch(int lobbyId)
    {
        Console.WriteLine($"[SignalR] Completing match for lobby {lobbyId}");

        var lobby = _lobbiesManager.GetLobby(lobbyId);

        if (lobby == null || !lobby.IsGameServerAllocated)
        {
            return Task.CompletedTask;
        }

        _gameServerAllocator.Release(lobby.GameServerPort);
        _lobbiesManager.RemoveLobby(lobbyId);

        return Task.CompletedTask;
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
        var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        if (!lobby.TryStartAllocationTimer(cancellationTokenSource))
        {
            cancellationTokenSource.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_matchmakingSettings.TimeoutSeconds), cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await TryAllocateLobbyAsync(lobby);
        });
    }

    private async Task TryAllocateLobbyAsync(Lobby lobby)
    {
        if (lobby.IsMatchFinalized || lobby.MemberCount == 0)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - lobby.CreatedAtUtc;
        if (!lobby.IsFull && elapsed.TotalSeconds < _matchmakingSettings.TimeoutSeconds)
        {
            return;
        }

        if (!_gameServerAllocator.TryAllocate(out var port))
        {
            await _hubContext.Clients.Group(GetLobbyGroupName(lobby.Id))
                .SendAsync("MatchmakingError", "No available game server ports");
            return;
        }

        if (!lobby.TryFinalizeMatch(_gameServerSettings.Host, port))
        {
            _gameServerAllocator.Release(port);
            return;
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
    }

    private string GetLobbyGroupName(int lobbyId)
    {
        return $"lobby_{lobbyId}";
    }
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
