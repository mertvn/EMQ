using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Server.Db;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Erodle.Entities.Concrete;
using EMQ.Shared.Erodle.Entities.Concrete.Dto.Request;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.PlayQuiz)]
[ApiController]
[Route("[controller]")]
public class ErodleController : ControllerBase
{
    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("GetErodleContainer")]
    public async Task<ActionResult<ErodleContainer>> GetErodleContainer(ReqGetErodle req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        if (req.UserId != session.Player.Id)
        {
            var status =
                await connection.QuerySingleOrDefaultAsync<ErodleStatus?>(
                    "select status from erodle e left join erodle_users eu on eu.erodle_id = e.id where date = @Date and kind = @Kind and user_id = @userId",
                    new { req.Date, req.Kind, userId = session.Player.Id });
            if (status is null or <= 0)
            {
                return Unauthorized();
            }
        }

        var erodle =
            await connection.QuerySingleOrDefaultAsync<Erodle>(
                "select * from erodle where date = @Date and kind = @Kind", new { req.Date, req.Kind });
        if (erodle != null)
        {
            // todo? join
            var status = await connection.QuerySingleOrDefaultAsync<ErodleStatus?>(
                @"select status from erodle_users where erodle_id = @erodleId and user_id = @UserId",
                new { erodleId = erodle.id, req.UserId });

            var erodleContainer = new ErodleContainer { Erodle = erodle, Status = status ?? ErodleStatus.Playing };

            var erodleHistories =
                (await connection.QueryAsync<ErodleHistory>(
                    "select * from erodle_history where erodle_id = @id and user_id = @UserId",
                    new { erodle.id, req.UserId })).ToArray();
            if (erodleHistories.Any())
            {
                var mIdSongSources = await DbManager.SelectSongSourceBatch(connection,
                    erodleHistories
                        .Select(x => new Song { Sources = new List<SongSource> { new() { Id = int.Parse(x.guess) } } })
                        .ToList(), true);

                var songSourcesDict = new Dictionary<int, SongSource>();
                foreach ((_, Dictionary<int, SongSource> value) in mIdSongSources)
                {
                    foreach ((int key, var songSource) in value)
                    {
                        if (!songSourcesDict.TryGetValue(key, out _))
                        {
                            songSourcesDict.Add(key, songSource);
                        }
                    }
                }

                var previousAnswers = erodleHistories.Select(x =>
                {
                    var songSource = songSourcesDict[int.Parse(x.guess)]; // todo? tryget
                    var title = songSource.Titles.FirstOrDefault(y => y.Language == "ja" && y.IsMainTitle) ??
                                songSource.Titles.First();
                    return new ErodleAnswer
                    {
                        ErodleId = x.erodle_id,
                        GuessNumber = x.sp,
                        AutocompleteMst = new AutocompleteMst(songSource.Id, title.LatinTitle),
                        Date = songSource.AirDateStart.Date,
                        Tags = songSource.Categories.Where(y => y.SpoilerLevel == SpoilerLevel.None)
                            .OrderByDescending(y => y.Rating).ToList(),
                        Developers = songSource.Developers,
                        Rating = songSource.RatingAverage,
                        VoteCount = songSource.VoteCount
                    };
                });
                erodleContainer.PreviousAnswers = previousAnswers.ToList();
            }

            return erodleContainer;
        }

        return NotFound();
    }

    // todo? don't allow submissions if status is not Playing
    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("SubmitAnswer")]
    public async Task<ActionResult> SubmitAnswer(ErodleAnswer req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var erodleHistory = new ErodleHistory
        {
            erodle_id = req.ErodleId,
            user_id = session.Player.Id,
            sp = req.GuessNumber,
            guess = req.AutocompleteMst.MSId.ToString()
        };
        bool success = await connection.InsertAsync(erodleHistory);
        return success ? Ok() : StatusCode(520);
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("UpsertStatus")]
    public async Task<ActionResult<List<ErodleAnswer>>> UpsertStatus(ReqUpsertStatus req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        bool success = await connection.UpsertAsync(new ErodleUsers
        {
            erodle_id = req.ErodleId, user_id = session.Player.Id, status = req.Status
        });
        return success ? Ok() : StatusCode(520);
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("GetLeaderboards")]
    public async Task<ActionResult<ErodlePlayerInfo[]>> GetLeaderboards(ReqGetErodle? req)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

        var usernamesDict = (await connectionAuth.QueryAsync<(int, string)>("select id, username from users"))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

        const string sql = @"WITH totals AS (
SELECT user_id, COUNT(CASE WHEN status = 2 THEN 1 END) AS Wins, COUNT(CASE WHEN status = 1 THEN 1 END) AS Losses, COUNT(status) AS Plays
FROM erodle_users eu
JOIN erodle e ON e.id = eu.erodle_id
WHERE ((@date::date IS NULL) or e.date = @date::date) AND user_id < 1000000
GROUP BY user_id
),
g AS (
SELECT user_id, COUNT(*) AS guesses FROM erodle_history eh JOIN erodle e ON e.id = eh.erodle_id WHERE ((@date::date IS NULL) or e.date = @date::date) GROUP BY user_id
)
SELECT totals.user_id as UserId, Wins, Losses, Plays,
COALESCE(guesses, 0) AS Guesses, ROUND(COALESCE(((1.0 * guesses)/plays), 0), 2) AS AvgGuesses
FROM totals LEFT JOIN g ON totals.user_id = g.user_id
ORDER BY wins desc, avgguesses";

        var res = (await connection.QueryAsync<ErodlePlayerInfo>(sql, new { date = req?.Date })).ToArray();
        foreach (ErodlePlayerInfo erodlePlayerInfo in res)
        {
            erodlePlayerInfo.Username = Utils.UserIdToUsername(usernamesDict, erodlePlayerInfo.UserId);
        }

        return res;
    }
}
