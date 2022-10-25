using System;
using Npgsql;

namespace BlazorApp1.Server;

public static class ConnectionHelper
{
    public static string GetConnectionString()
    {
        string? databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new Exception("Error getting DATABASE_URL envvar");
        }
        return BuildConnectionString(databaseUrl);
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
            TrustServerCertificate = true
        };
        return builder.ToString();
    }
}
