using System.Collections.Generic;
using System.Linq;

namespace StupidGuysServer.Services
{
    public class GameServerAllocator
    {
        private readonly SortedSet<int> _availablePorts;
        private readonly HashSet<int> _allocatedPorts = new();
        private readonly object _lock = new();

        public GameServerAllocator(int portRangeStart, int portRangeEnd)
        {
            var ports = Enumerable.Range(portRangeStart, portRangeEnd - portRangeStart + 1);

            _availablePorts = new SortedSet<int>(ports);
        }

        public bool TryAllocate(out int port)
        {
            lock (_lock)
            {
                if (_availablePorts.Count == 0)
                {
                    port = 0;
                    return false;
                }

                port = _availablePorts.Min;
                _availablePorts.Remove(port);
                _allocatedPorts.Add(port);
                return true;
            }
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
    }
}
