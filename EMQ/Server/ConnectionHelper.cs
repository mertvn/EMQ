using System;
using Npgsql;

namespace EMQ.Server;

public static class ConnectionHelper
{
    private static string s_cachedCnnStr = "";

    public static string GetConnectionString()
    {
        string? databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new Exception("Error getting DATABASE_URL envvar");
        }

        return !string.IsNullOrWhiteSpace(s_cachedCnnStr) ? s_cachedCnnStr : BuildConnectionString(databaseUrl);
    }

    private static string BuildConnectionString(string databaseUrl)
    {
        var databaseUri = new Uri(databaseUrl);
        string[] userInfo = databaseUri.UserInfo.Split(':');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.Port,
            Username = userInfo[0],
            Password = userInfo[1],
            Database = databaseUri.LocalPath.TrimStart('/'),
            SslMode = SslMode.Prefer,
            TrustServerCertificate = true,
            IncludeErrorDetail = true
        };

        string str = builder.ToString();
        s_cachedCnnStr = str;
        return str;
    }
}
