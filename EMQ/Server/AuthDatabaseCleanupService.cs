using System;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Db;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
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
        return await DbManager.DeleteExpiredVerificationRows();
    }
}
