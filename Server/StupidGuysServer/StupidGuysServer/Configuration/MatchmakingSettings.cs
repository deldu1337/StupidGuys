using System;

namespace StupidGuysServer.Configuration
{
    public class MatchmakingSettings
    {
        public MatchmakingSettings(int timeoutSeconds, int portRangeStart, int portRangeEnd)
        {
            TimeoutSeconds = timeoutSeconds;
            PortRangeStart = portRangeStart;
            PortRangeEnd = portRangeEnd;
        }

        public int TimeoutSeconds { get; }
        public int PortRangeStart { get; }
        public int PortRangeEnd { get; }

        public static MatchmakingSettings FromEnvironment()
        {
            var timeoutValue = Environment.GetEnvironmentVariable("MATCH_TIMEOUT_SECONDS");
            var timeoutSeconds = int.TryParse(timeoutValue, out var parsedTimeout) ? parsedTimeout : 60;

            var portStartValue = Environment.GetEnvironmentVariable("ALLOCATION_PORT_START");
            var portEndValue = Environment.GetEnvironmentVariable("ALLOCATION_PORT_END");
            var portStart = int.TryParse(portStartValue, out var parsedPortStart) ? parsedPortStart : 7778;
            var portEnd = int.TryParse(portEndValue, out var parsedPortEnd) ? parsedPortEnd : 7779;

            if (portEnd < portStart)
            {
                (portStart, portEnd) = (portEnd, portStart);
            }

            return new MatchmakingSettings(timeoutSeconds, portStart, portEnd);
        }
    }
}
