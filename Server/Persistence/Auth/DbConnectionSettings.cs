using Microsoft.EntityFrameworkCore;

namespace Auth
{
    public static class DbConnectionSettings
    {
        public const string CONNECTION =
            "server=127.0.0.1;" +
            "port=3309;" +
            "user=master;" +
            "password=1234;" +
            "database=game;";

        public static readonly MySqlServerVersion MYSQL_SERVER_VERSION = new MySqlServerVersion(new Version(8, 4));
    }
}
