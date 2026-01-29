using StupidGuysServer.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StupidGuysServer.Services
{
    public class LobbiesManager
    {
        private readonly ConcurrentDictionary<int, Lobby> _lobbies = new();
        private readonly ConcurrentQueue<int> _availableLobbyIds;
        private readonly HashSet<int> _activeLobbyIds = new();
        private readonly object _idLock = new();

        public LobbiesManager(int maxLobbyCount)
        {
            _availableLobbyIds = new ConcurrentQueue<int>(Enumerable.Range(1, maxLobbyCount));
        }

        public Lobby? FindAvailableLobby()
        {
            return _lobbies.Values.FirstOrDefault(lobby => !lobby.IsFull && !lobby.IsMatchFinalized);
        }

        public Lobby? CreateLobby(int maxPlayers)
        {
            lock (_idLock)
            {
                if (!_availableLobbyIds.TryDequeue(out var lobbyId))
                {
                    return null;
                }

                _activeLobbyIds.Add(lobbyId);

                var lobby = new Lobby(lobbyId, maxPlayers);
                _lobbies[lobbyId] = lobby;

                return lobby;
            }
        }

        public Lobby? GetLobby(int lobbyId)
        {
            _lobbies.TryGetValue(lobbyId, out var lobby);
            return lobby;
        }

        public Lobby? RemovePlayerFromAllLobbies(string connectionId)
        {
            foreach (var lobby in _lobbies.Values)
            {
                if (lobby.IsMatchFinalized)
                {
                    continue;
                }

                if (lobby.TryRemoveMember(connectionId, out int remainCount))
                {
                    if (remainCount == 0)
                    {
                        RemoveLobby(lobby.Id);
                    }
                    return lobby;
                }
            }
            return null;
        }

        public bool RemoveLobby(int lobbyId)
        {
            if (!_lobbies.TryRemove(lobbyId, out _))
            {
                return false;
            }

            lock (_idLock)
            {
                if (_activeLobbyIds.Remove(lobbyId))
                {
                    _availableLobbyIds.Enqueue(lobbyId);
                }
            }

            return true;
        }
    }
}
