using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace StupidGuysServer.Services
{
    public class GameServerAllocator
    {
        private readonly SortedSet<int> _availablePorts;
        private readonly HashSet<int> _allocatedPorts = new();
        private readonly object _lock = new();
        private readonly string _gameServerHost;

        public GameServerAllocator(string gameServerHost, int portRangeStart, int portRangeEnd)
        {
            _gameServerHost = gameServerHost;
            _availablePorts = new SortedSet<int>(Enumerable.Range(portRangeStart, portRangeEnd - portRangeStart + 1));
        }

        public bool TryAllocate(out int port)
        {
            lock (_lock)
            {
                foreach (var candidate in _availablePorts.ToList())
                {
                    if (!IsPortReachable(_gameServerHost, candidate))
                    {
                        continue;
                    }

                    _availablePorts.Remove(candidate);
                    _allocatedPorts.Add(candidate);

                    port = candidate;
                    return true;
                }
            }

            port = 0;
            return false;
        }

        public void Release(int port)
        {
            lock (_lock)
            {
                if (_allocatedPorts.Remove(port))
                {
                    _availablePorts.Add(port);
                }
            }
        }

        private static bool IsPortReachable(string host, int port)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(host, port);
                var completed = connectTask.Wait(TimeSpan.FromMilliseconds(300));

                return completed && tcpClient.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
