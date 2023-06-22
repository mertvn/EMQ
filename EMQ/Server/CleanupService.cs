using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server;

public sealed class CleanupService : IHostedService, IDisposable
{
    private readonly ILogger<CleanupService> _logger;
    private Timer? _timer;

    public CleanupService(ILogger<CleanupService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CleanupService is starting");
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
        // _logger.LogInformation("CleanupService is working. Count: {Count}", count);

        foreach (Room room in ServerState.Rooms)
        {
            var roomSessions = ServerState.Sessions.Where(x => room.Players.Any(y => y.Id == x.Player.Id)).ToList();
            var activeSessions = roomSessions
                .Where(x => (DateTime.UtcNow - x.Player.LastHeartbeatTimestamp) < TimeSpan.FromMinutes(5)).ToList();
            if (!activeSessions.Any()
                //  && (room.Quiz == null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing) // not sure if we need this
               )
            {
                ServerState.RemoveRoom(room, "CleanupService");
                continue;
            }

            if (room.Quiz == null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing)
            {
                var inactiveSessions = roomSessions.Except(activeSessions);
                foreach (Session inactiveSession in inactiveSessions)
                {
                    Console.WriteLine(
                        $"Cleaning up p{inactiveSession.Player.Id} {inactiveSession.Player.Username} from r{room.Id} {room.Name}");

                    if (room.Spectators.Any(x => x.Id == inactiveSession.Player.Id))
                    {
                        room.RemoveSpectator(inactiveSession.Player);
                    }
                    else
                    {
                        room.RemovePlayer(inactiveSession.Player);
                    }

                    room.AllConnectionIds.Remove(inactiveSession.Player.Id, out _);
                    room.Log($"{inactiveSession.Player.Username} was removed from the room due to inactivity.", -1,
                        true);
                }
                // todo make players spectators if they are connected but AFK
            }
        }
    }
}
