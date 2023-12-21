using System;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server;

public sealed class EmailQueueService : BackgroundService
{
    private readonly ILogger<EmailQueueService> _logger;

    public EmailQueueService(ILogger<EmailQueueService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailQueueService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWork();
        }
    }

    private static async Task DoWork()
    {
        try
        {
            while (ServerState.EmailQueue.TryDequeue(out EmailQueueItem? emailQueueItem))
            {
                await EmailManager.SendEmail(emailQueueItem.MimeMessage, emailQueueItem.Description);
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
