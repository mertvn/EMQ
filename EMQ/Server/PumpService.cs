using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Hubs;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server;

public sealed class PumpService : BackgroundService
{
    private readonly ILogger<PumpService> _logger;
    private readonly IHubContext<QuizHub> _hubContext;

    public PumpService(ILogger<PumpService> logger, IHubContext<QuizHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PumpService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            DoWork();
        }
    }

    // Starts a dedicated thread for each player with a session in order to send Server -> Client messages.
    private void DoWork()
    {
        foreach (Session session in ServerState.Sessions)
        {
            int playerId = session.Player.Id;
            if (ServerState.PumpThreads.TryGetValue(playerId, out _))
            {
                continue;
            }

            // this might cause a memory leak, but there's really no good way to dispose it the way we're using it
            // todo? remove it because we're not using it
            var tokenSource = new CancellationTokenSource();

            Thread thread = new(() => Pump(playerId, tokenSource.Token))
            {
                CurrentCulture = CultureInfo.InvariantCulture,
                CurrentUICulture = CultureInfo.InvariantCulture,
                IsBackground = true,
                Name = $"EMQPUMP_p{playerId}",
                Priority = ThreadPriority.Normal
            };

            while (!ServerState.PumpThreads.ContainsKey(playerId))
            {
                // Console.WriteLine($"{DateTime.UtcNow:O} attempting to create thread for p{playerId}");
                ServerState.PumpThreads.TryAdd(playerId, thread);
            }

            thread.Start();
        }
    }

    private void Pump(int playerId, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Session? session = null;
                foreach (Session x in ServerState.Sessions)
                {
                    if (x.Player.Id == playerId)
                    {
                        session = x;
                        break;
                    }
                }

                if (session is null)
                {
                    break;
                }

                bool sentAtLeastOneMessage = false;
                if (ServerState.PumpMessages.TryGetValue(playerId, out var queue))
                {
                    while (queue.TryDequeue(out var message))
                    {
                        sentAtLeastOneMessage = true;

                        // if (message.Target == "ReceiveCorrectAnswer")
                        // {
                        //     Console.WriteLine(
                        //         $"{DateTime.UtcNow:O} attempting to send {message.Target} message for {playerId}");
                        // }

                        // todo max timeout per message
                        // Console.WriteLine($"{DateTime.UtcNow:O} attempting to send {message.Target} message for {playerId}");
                        _hubContext.Clients.Client(session.ConnectionId!)
                            .SendCoreAsync(message.Target, message.Arguments, token).GetAwaiter().GetResult();
                        Thread.Sleep(5); // let clients render i guess
                    }
                }

                if (!sentAtLeastOneMessage)
                {
                    Thread.Sleep((int)Quiz.TickRate);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            while (ServerState.PumpThreads.ContainsKey(playerId))
            {
                Console.WriteLine($"{DateTime.UtcNow:O} attempting to remove thread for {playerId}");
                ServerState.PumpThreads.TryRemove(playerId, out _);
            }
        }
    }
}
