using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace StupidGuysServer.Services
{
    public class GameServerAllocator
    {
        private readonly SortedSet<int> _portPool;
        private readonly HashSet<int> _allocatedPorts = new();
        private readonly Func<int, bool> _isPortInUse;
        private readonly object _lock = new();

        public GameServerAllocator(int portRangeStart, int portRangeEnd)
            : this(portRangeStart, portRangeEnd, IsPortInUse)
        {
        }

        internal GameServerAllocator(int portRangeStart, int portRangeEnd, Func<int, bool> isPortInUse)
        {
            var ports = Enumerable.Range(portRangeStart, portRangeEnd - portRangeStart + 1);
            _portPool = new SortedSet<int>(ports);
            _isPortInUse = isPortInUse;
        }

        public bool TryAllocate(out int port)
        {
            lock (_lock)
            {
                foreach (var candidate in _portPool)
                {
                    if (_allocatedPorts.Contains(candidate))
                    {
                        continue;
                    }

                    if (!_isPortInUse(candidate))
                    {
                        continue;
                    }

                    if (_allocatedPorts.Add(candidate))
                    {
                        port = candidate;
                        return true;
                    }
                }
            }

            port = 0;
            return false;
        }

        public void Release(int port)
        {
            lock (_lock)
            {
                _allocatedPorts.Remove(port);
            }
        }

        private static bool IsPortInUse(int port)
        {
            return IsPortInUseByTcp(port) || IsPortInUseByUdp(port);
        }

        private static bool IsPortInUseByTcp(int port)
        {
            return !CanBindTcp(port);
        }

        private static bool IsPortInUseByUdp(int port)
        {
            return !CanBindUdp(port);
        }

        private static bool CanBindTcp(int port)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static bool CanBindUdp(int port)
        {
            try
            {
                using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
