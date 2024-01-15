using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using EMQ.Shared.Core;
using Npgsql;

namespace EMQ.Server;

public static class ConnectionHelper
{
    private static readonly ConcurrentDictionary<string, string> s_cachedCnnStr = new();

    private static string GetDatabaseUrl(string envVar)
    {
        string? databaseUrl = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new Exception($"Error getting {envVar} envVar");
        }

        return databaseUrl;
    }

    public static string GetConnectionString()
    {
        const string db = "DATABASE_URL"; // todo rename to EMQ_SONG_DATABASE_URL
        if (s_cachedCnnStr.TryGetValue(db, out string? cnnStr))
        {
            return cnnStr;
        }
        else
        {
            string databaseUrl = GetDatabaseUrl(db);
            NpgsqlConnectionStringBuilder builder = GetConnectionStringBuilderWithDatabaseUrl(databaseUrl);
            string str = builder.ToString();
            s_cachedCnnStr[db] = str;
            return str;
        }
    }

    public static string GetConnectionString_Auth()
    {
        const string db = "EMQ_AUTH_DATABASE_URL";
        if (s_cachedCnnStr.TryGetValue(db, out string? cnnStr))
        {
            return cnnStr;
        }
        else
        {
            string databaseUrl = GetDatabaseUrl(db);
            NpgsqlConnectionStringBuilder builder = GetConnectionStringBuilderWithDatabaseUrl(databaseUrl);
            string str = builder.ToString();
            s_cachedCnnStr[db] = str;
            return str;
        }
    }

    public static NpgsqlConnectionStringBuilder GetConnectionStringBuilderWithDatabaseUrl(string databaseUrl)
    {
        var builder = GetConnectionStringBuilderInner(databaseUrl);
        return builder;
    }

    public static NpgsqlConnectionStringBuilder GetConnectionStringBuilderWithEnvVar(string envVar)
    {
        string databaseUrl = GetDatabaseUrl(envVar);
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

        // Console.WriteLine(JsonSerializer.Serialize(builder, Utils.JsoIndented));
        return builder;
    }
}
