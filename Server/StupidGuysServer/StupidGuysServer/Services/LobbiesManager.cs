using StupidGuysServer.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StupidGuysServer.Services
{
    public class LobbiesManager
    {
        private readonly ConcurrentDictionary<int, Lobby> _lobbies = new();
        private int _nextLobbyId = 1;
        private readonly SortedSet<int> _reusableLobbyIds = new();
        private readonly object _idLock = new object();

        public Lobby? FindAvailableLobby()
        {
            return _lobbies.Values.FirstOrDefault(lobby => !lobby.IsFull && !lobby.IsMatchFinalized);
        }

        public Lobby CreateLobby(int maxPlayers)
        {
            int lobbyId;
            lock (_idLock)
            {
                if (_reusableLobbyIds.Count > 0)
                {
                    lobbyId = _reusableLobbyIds.Min;
                    _reusableLobbyIds.Remove(lobbyId);
                }
                else
                {
                    lobbyId = _nextLobbyId++;
                }
            }

            var lobby = new Lobby(lobbyId, maxPlayers);
            _lobbies[lobbyId] = lobby;

            return lobby;
        }

        public Lobby? GetLobby(int lobbyId)
        {
            _lobbies.TryGetValue(lobbyId, out var lobby);
            return lobby;
        }


        public Lobby? FindLobbyByConnectionId(string connectionId)
        {
            return _lobbies.Values.FirstOrDefault(lobby => lobby.ContainsMember(connectionId));
        }

        public Lobby? RemovePlayerFromAllLobbies(string connectionId)
        {
            foreach (var lobby in _lobbies.Values)
            {
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
                _reusableLobbyIds.Add(lobbyId);
            }

            return true;
        }
    }
}
