using StupidGuysServer.Models;
using System.Collections.Concurrent;

namespace StupidGuysServer.Services
{
    public class LobbiesManager
    {
        private const int FixedLobbyId = 1;

        private readonly ConcurrentDictionary<int, Lobby> _lobbies = new();

        public Lobby? FindAvailableLobby()
        {
            if (_lobbies.TryGetValue(FixedLobbyId, out var lobby) && !lobby.IsFull && !lobby.IsMatchFinalized)
            {
                return lobby;
            }

            return null;
        }

        public Lobby CreateLobby(int maxPlayers)
        {
            var lobby = new Lobby(FixedLobbyId, maxPlayers);
            _lobbies[FixedLobbyId] = lobby;
            return lobby;
        }

        public Lobby? GetLobby(int lobbyId)
        {
            _lobbies.TryGetValue(lobbyId, out var lobby);
            return lobby;
        }

        public Lobby? RemovePlayerFromAllLobbies(string connectionId)
        {
            if (!_lobbies.TryGetValue(FixedLobbyId, out var lobby))
            {
                return null;
            }

            if (lobby.IsMatchFinalized)
            {
                return null;
            }

            if (lobby.TryRemoveMember(connectionId, out int remainCount))
            {
                if (remainCount == 0)
                {
                    _lobbies.TryRemove(FixedLobbyId, out _);
                }

                return lobby;
            }

            return null;
        }

        public bool RemoveLobby(int lobbyId)
        {
            return _lobbies.TryRemove(lobbyId, out _);
        }
    }
}
