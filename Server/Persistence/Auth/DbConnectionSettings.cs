using Microsoft.EntityFrameworkCore;

namespace Auth
{
    public static class DbConnectionSettings
    {
        // PostgreSQL 로컬 연결 문자열
        public const string CONNECTION =
            "Host=127.0.0.1;" +
            "Port=5432;" +
            "Username=postgres;" +
            "Password=1234;" +
            "Database=game;";
    }
}
