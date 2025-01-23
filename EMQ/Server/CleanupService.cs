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

public sealed class CleanupService : BackgroundService
{
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(ILogger<CleanupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CleanupService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            DoWork();
        }
    }

    private static void DoWork()
    {
        // _logger.LogInformation("CleanupService is working. Count: {Count}", count);
        foreach (Room room in ServerState.Rooms)
        {
            if ((DateTime.UtcNow - room.CreatedAt) < TimeSpan.FromMinutes(1))
            {
                continue;
            }

            var roomSessions = ServerState.Sessions.Where(x =>
                room.Players.Any(y => y.Id == x.Player.Id) || room.Spectators.Any(y => y.Id == x.Player.Id)).ToList();
            var activeSessions = roomSessions
                .Where(x => (DateTime.UtcNow - x.Player.LastHeartbeatTimestampQuiz) < TimeSpan.FromMinutes(2)).ToList();
            if (!activeSessions.Any()
                //  && (room.Quiz == null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing) // not sure if we need this
               )
            {
                ServerState.RemoveRoom(room, "CleanupService1");
                continue;
            }

            if (room.Quiz == null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing)
            {
                var inactiveSessions = roomSessions.Except(activeSessions);
                foreach (Session inactiveSession in inactiveSessions)
                {
                    Console.WriteLine(
                        $"Cleaning up p{inactiveSession.Player.Id} {inactiveSession.Player.Username} from r{room.Id} {room.Name}");

                    room.Log($"{inactiveSession.Player.Username} was removed from the room due to inactivity.", -1,
                        true);

                    if (room.Spectators.Any(x => x.Id == inactiveSession.Player.Id))
                    {
                        room.RemoveSpectator(inactiveSession.Player);
                    }
                    else
                    {
                        room.RemovePlayer(inactiveSession.Player);
                        if (!room.Players.Any(x => !x.IsBot))
                        {
                            ServerState.RemoveRoom(room, "CleanupService2");
                        }
                        else
                        {
                            if (room.Owner.Id == inactiveSession.Player.Id)
                            {
                                var newOwner = room.Players.First(x => !x.IsBot);
                                room.Owner = newOwner;
                                room.Log($"{newOwner.Username} is the new owner.", -1, true);
                            }
                        }
                    }
                }
                // todo make players spectators if they are connected but AFK
            }
        }

        foreach (Session session in ServerState.Sessions)
        {
            if ((DateTime.UtcNow - session.CreatedAt) < TimeSpan.FromMinutes(1))
            {
                continue;
            }

            foreach ((string key, PlayerConnectionInfo? value) in session.PlayerConnectionInfos)
            {
                if ((DateTime.UtcNow - value.LastHeartbeatTimestamp) > TimeSpan.FromMinutes(2))
                {
                    Console.WriteLine(
                        $"Evicting inactive connection {key} from memory p{session.Player.Id} {session.Player.Username}");
                    while (session.PlayerConnectionInfos.ContainsKey(key))
                    {
                        session.PlayerConnectionInfos.Remove(key, out _);
                    }
                }
            }

            bool isInARoom =
                ServerState.Rooms.FirstOrDefault(x =>
                    x.Players.Any(y => y.Id == session.Player.Id) ||
                    x.Spectators.Any(y => y.Id == session.Player.Id)) != null;
            if ((DateTime.UtcNow - session.Player.LastHeartbeatTimestamp) > TimeSpan.FromMinutes(30) && !isInARoom)
            {
                Console.WriteLine(
                    $"Evicting inactive session from memory p{session.Player.Id} {session.Player.Username}");
                ServerState.RemoveSession(session, "CleanupService3");
            }
        }
    }
}
