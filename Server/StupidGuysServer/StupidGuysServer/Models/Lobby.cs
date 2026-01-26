using System;
using System.Collections.Generic;
using System.Threading;

namespace StupidGuysServer.Models
{
    public class Lobby
    {
        private readonly object _gate = new object();
        private readonly HashSet<string> _members = new();
        private bool _allocationTimerStarted;

        public int Id { get; }
        public int MaxPlayers { get; }
        public DateTime CreatedAtUtc { get; }
        public string? GameServerIP { get; set; }
        public int GameServerPort { get; set; }
        public bool IsGameServerAllocated { get; set; }
        public bool IsMatchFinalized { get; private set; }
        public CancellationTokenSource? AllocationCancellation { get; private set; }

        public Lobby(int id, int maxPlayers)
        {
            Id = id;
            MaxPlayers = maxPlayers;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public int MemberCount
        {
            get
            {
                lock (_gate)
                {
                    return _members.Count;
                }
            }
        }

        public bool IsFull
        {
            get
            {
                lock (_gate)
                {
                    return _members.Count >= MaxPlayers;
                }
            }
        }

        public bool TryAddMember(string connectionId, out int remainMemberCount)
        {
            lock (_gate)
            {
                if (IsMatchFinalized)
                {
                    remainMemberCount = 0;
                    return false;
                }

                if (_members.Count >= MaxPlayers)
                {
                    remainMemberCount = 0;
                    return false;
                }

                bool added = _members.Add(connectionId);
                remainMemberCount = MaxPlayers - _members.Count;
                return added;
            }
        }

        public bool TryRemoveMember(string connectionId, out int remainMemberCount)
        {
            lock (_gate)
            {
                bool removed = _members.Remove(connectionId);
                remainMemberCount = _members.Count;
                return removed;
            }
        }

        public bool TryStartAllocationTimer(CancellationTokenSource cancellationTokenSource)
        {
            lock (_gate)
            {
                if (_allocationTimerStarted)
                {
                    return false;
                }

                _allocationTimerStarted = true;
                AllocationCancellation = cancellationTokenSource;
                return true;
            }
        }

        public bool TryFinalizeMatch(string host, int port)
        {
            lock (_gate)
            {
                if (IsMatchFinalized)
                {
                    return false;
                }

                GameServerIP = host;
                GameServerPort = port;
                IsGameServerAllocated = true;
                IsMatchFinalized = true;
                return true;
            }
        }
    }
}
