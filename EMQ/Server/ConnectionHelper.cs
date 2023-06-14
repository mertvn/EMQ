using System;
using Npgsql;

namespace EMQ.Server;

public static class ConnectionHelper
{
    private static string s_cachedCnnStr = "";

    private static string GetDatabaseUrl()
    {
        string? databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new Exception("Error getting DATABASE_URL envvar");
        }

        return databaseUrl;
    }

    public static string GetConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(s_cachedCnnStr))
        {
            return s_cachedCnnStr;
        }
        else
        {
            string databaseUrl = GetDatabaseUrl();
            NpgsqlConnectionStringBuilder builder = GetConnectionStringBuilder(databaseUrl);
            string str = builder.ToString();
            s_cachedCnnStr = str;
            return str;
        }
    }

    public static NpgsqlConnectionStringBuilder GetConnectionStringBuilder(string databaseUrl)
    {
        var builder = GetConnectionStringBuilderInner(databaseUrl);
        return builder;
    }

    public static NpgsqlConnectionStringBuilder GetConnectionStringBuilder()
    {
        string databaseUrl = GetDatabaseUrl();
        var builder = GetConnectionStringBuilderInner(databaseUrl);
        return builder;
    }

    private static NpgsqlConnectionStringBuilder GetConnectionStringBuilderInner(string databaseUrl)
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

        return builder;
    }
}
