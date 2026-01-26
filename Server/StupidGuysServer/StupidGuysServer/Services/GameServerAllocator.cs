using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StupidGuysServer.Services
{
    public class GameServerAllocator
    {
        private readonly ConcurrentQueue<int> _availablePorts;
        private readonly HashSet<int> _allocatedPorts = new();
        private readonly object _lock = new();

        public GameServerAllocator(int portRangeStart, int portRangeEnd)
        {
            var ports = Enumerable.Range(portRangeStart, portRangeEnd - portRangeStart + 1);
            _availablePorts = new ConcurrentQueue<int>(ports);
        }

        public bool TryAllocate(out int port)
        {
            lock (_lock)
            {
                while (_availablePorts.TryDequeue(out var candidate))
                {
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
                if (_allocatedPorts.Remove(port))
                {
                    _availablePorts.Enqueue(port);
                }
            }
        }
    }
}
