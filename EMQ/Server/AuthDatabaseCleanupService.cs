using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EMQ.Server;

public sealed class AuthDatabaseCleanupService : BackgroundService
{
    private readonly ILogger<AuthDatabaseCleanupService> _logger;

    public AuthDatabaseCleanupService(ILogger<AuthDatabaseCleanupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuthDatabaseCleanupService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int totalAffectedRows = await DoWork();
            if (totalAffectedRows > 0)
            {
                _logger.LogInformation($"AuthDatabaseCleanupService cleaned up {totalAffectedRows} rows");
            }
        }
    }

    private static async Task<int> DoWork()
    {
        // todo delete users_label rows belonging to inexisting guests

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var unregisterQueues = await connectionAuth.QueryAsync<UnregisterQueue>(
            $"select * from unregister_queue where created_at < (select now()) - interval '{AuthStuff.UnregisterDays} days'");
        foreach (UnregisterQueue unregisterQueue in unregisterQueues)
        {
            try
            {
                await AuthManager.UnregisterStep3DeleteAccount(unregisterQueue);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        return await DbManager_Auth.DeleteExpiredVerificationRows();
    }
}
