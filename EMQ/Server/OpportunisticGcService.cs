using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server;

public sealed class OpportunisticGcService : BackgroundService
{
    private readonly ILogger<OpportunisticGcService> _logger;

    private static DateTime LastGc { get; set; } = DateTime.UtcNow;

    public OpportunisticGcService(ILogger<OpportunisticGcService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpportunisticGcService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            DoWork();
        }
    }

    private static void DoWork()
    {
        if (!ServerState.Rooms.Any(
                x => x.Quiz?.QuizState.QuizStatus == QuizStatus.Playing && !x.Quiz.QuizState.IsPaused) &&
            DateTime.UtcNow - LastGc > TimeSpan.FromMinutes(1))
        {
            LastGc = DateTime.UtcNow;
            ServerUtils.RunAggressiveGc();
        }
    }
}
