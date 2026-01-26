using System;

namespace StupidGuysServer.Configuration
{
    public class GameServerSettings
    {
        public GameServerSettings(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }

        public static GameServerSettings FromEnvironment()
        {
            var host = Environment.GetEnvironmentVariable("GAME_SERVER_HOST")
                       ?? Environment.GetEnvironmentVariable("GAME_SERVER_IP")
                       ?? "127.0.0.1";

            var portValue = Environment.GetEnvironmentVariable("GAME_SERVER_PORT");
            var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 7777;

            return new GameServerSettings(host, port);
        }
    }
}
