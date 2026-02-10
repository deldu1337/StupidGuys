using System.Collections.Generic;
using System.Linq;

namespace StupidGuysServer.Services
{
    public class GameServerAllocator
    {
        private readonly SortedSet<int> _portPool;
        private readonly HashSet<int> _allocatedPorts = new();
        private readonly object _lock = new();

        public GameServerAllocator(int portRangeStart, int portRangeEnd)
        {
            var ports = Enumerable.Range(portRangeStart, portRangeEnd - portRangeStart + 1);
            _portPool = new SortedSet<int>(ports);
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
    }
}
