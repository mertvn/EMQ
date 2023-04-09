using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server;

public sealed class CleanupService : IHostedService, IDisposable
{
    private int _executionCount;
    private readonly ILogger<CleanupService> _logger;
    private Timer? _timer;

    public CleanupService(ILogger<CleanupService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CleanupService is running");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CleanupService is stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void DoWork(object? state)
    {
        int count = Interlocked.Increment(ref _executionCount);
        _logger.LogInformation("CleanupService is working. Count: {Count}", count);

        Console.WriteLine(
            $"Rooms count: {ServerState.Rooms.Count}");
        Console.WriteLine(
            $"QuizManagers count: {ServerState.QuizManagers.Count}");
        Console.WriteLine(
            $"Sessions count: {ServerState.Sessions.Count(x => x.HasActiveConnection)}/{ServerState.Sessions.Count}");

        foreach (Room room in ServerState.Rooms.ToList())
        {
            var activeSessions = ServerState.Sessions.Where(x => room.Players.Any(y => y.Id == x.Player.Id))
                .Where(x => (DateTime.UtcNow - x.LastHeartbeatTimestamp) < TimeSpan.FromMinutes(5));

            if (!activeSessions.Any()
                //  && (room.Quiz == null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing) // not sure if we need this
               )
            {
                ServerState.CleanupRoom(room);
            }
            // todo make players spectators if they are AFK & and transfer room ownership if necessary
        }
    }
}
