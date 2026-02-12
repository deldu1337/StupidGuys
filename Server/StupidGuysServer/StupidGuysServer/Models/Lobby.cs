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

        // ✅ cleanup(Release/Remove) 중복 방지용
        private int _cleanupOnce = 0;

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
            get { lock (_gate) return _members.Count; }
        }

        public bool IsFull
        {
            get { lock (_gate) return _members.Count >= MaxPlayers; }
        }

        // ✅ 정리 루틴(Release/Remove)을 딱 1번만 실행하게 가드
        public bool TryBeginCleanup()
        {
            return Interlocked.CompareExchange(ref _cleanupOnce, 1, 0) == 0;
        }

        public bool TryAddMember(string connectionId, out int remainMemberCount)
        {
            lock (_gate)
            {
                // ✅ Finalized 된 로비에는 새 멤버 추가 금지
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
                    return false;

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
                    return false;

                GameServerIP = host;
                GameServerPort = port;
                IsGameServerAllocated = true;
                IsMatchFinalized = true;
                return true;
            }
        }
    }
}
