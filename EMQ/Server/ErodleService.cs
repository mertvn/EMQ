using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Erodle.Entities.Concrete;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EMQ.Server;

public sealed class ErodleService : BackgroundService
{
    private readonly ILogger<ErodleService> _logger;

    public ErodleService(ILogger<ErodleService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ErodleService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWork();
        }
    }

    private static async Task DoWork()
    {
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            foreach (var kind in Enum.GetValues<ErodleKind>().Where(x => x != ErodleKind.None))
            {
                var date = DateOnly.FromDateTime(DateTime.UtcNow);
                bool alreadyGeneratedToday =
                    await connection.ExecuteScalarAsync<bool>(
                        @"select 1 from erodle where date = @date and kind = @kind",
                        new { date, kind });
                if (alreadyGeneratedToday)
                {
                    continue;
                }

                var previousCorrectAnswers =
                    await connection.QueryAsync<string>(@"select correct_answer from erodle where kind = @kind",
                        new { kind });

                string correctAnswer;
                switch (kind)
                {
                    case ErodleKind.Mst:
                        int[] previous = previousCorrectAnswers.Select(x => int.Parse(x)).ToArray();
                        int[] possible = (await connection.QueryAsync<int>(
                            "select id from music_source where votecount >= @minVotes and not id = ANY(@previous)",
                            new { minVotes = Constants.ErodleMinVotes, previous })).ToArray();
                        Console.WriteLine($"possible erodle remaining: {possible.Length}");
                        correctAnswer = possible.Shuffle().First().ToString();
                        break;
                    case ErodleKind.None:
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var erodle = new Erodle { date = date, kind = kind, correct_answer = correctAnswer };
                Console.WriteLine($"inserting new Erodle {JsonSerializer.Serialize(erodle)}");
                await connection.InsertAsync(erodle);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
