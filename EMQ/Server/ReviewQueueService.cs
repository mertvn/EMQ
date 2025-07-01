using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.Extensions.Hosting;

namespace EMQ.Server;

public sealed class ReviewQueueService : BackgroundService
{
    public const int ToleranceMinutes = 120;

    public static string UnknownLineageRejectMessage { get; } =
        $"Automatically rejected for having unknown lineage {ToleranceMinutes} minutes after upload time.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("ReviewQueueService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWork();
        }
    }

    private static async Task DoWork()
    {
        try
        {
            var rqs = await DbManager.FindRQs(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1),
                SongSourceSongTypeMode.All, new[] { ReviewQueueStatus.Pending });
            foreach (RQ rq in rqs.Where(x => x.ShouldCheckLineage() &&
                                             x.lineage == SongLinkLineage.Unknown &&
                                             DateTime.UtcNow > x.submitted_on.AddMinutes(ToleranceMinutes)))
            {
                await DbManager.UpdateReviewQueueItem(rq.id, ReviewQueueStatus.Rejected, UnknownLineageRejectMessage);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
