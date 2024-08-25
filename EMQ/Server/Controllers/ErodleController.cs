using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Server.Db;
using EMQ.Shared.Auth.Entities.Concrete;
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
        var erodle =
            await connection.QuerySingleOrDefaultAsync<Erodle>(
                "select * from erodle where date = @Date and kind = @Kind", new { req.Date, req.Kind });
        if (erodle != null)
        {
            // todo? join
            var status = await connection.QuerySingleOrDefaultAsync<ErodleStatus?>(
                @"select status from erodle_users where erodle_id = @erodleId and user_id = @userId",
                new { erodleId = erodle.id, userId = session.Player.Id });
            return new ErodleContainer() { Erodle = erodle, Status = status ?? ErodleStatus.Playing };
        }

        return NotFound();
    }

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

    // todo? merge with GetErodleContainer
    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("GetPreviousAnswers")]
    public async Task<ActionResult<List<ErodleAnswer>>> GetPreviousAnswers([FromBody] int erodleId)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var erodleHistories =
            (await connection.QueryAsync<ErodleHistory>("select * from erodle_history where erodle_id = @erodleId",
                new { erodleId })).ToArray();
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
                        .OrderByDescending(y => y.Rating).Take(5).ToList(),
                    Developers = songSource.Developers,
                    Rating = songSource.RatingAverage,
                    VoteCount = songSource.VoteCount
                };
            });
            return previousAnswers.ToList();
        }

        return NotFound();
    }

    // [CustomAuthorize(PermissionKind.PlayQuiz)]
    // [HttpPost]
    // [Route("GetStatus")]
    // public async Task<ActionResult<ErodleStatus?>> GetStatus(int erodleId)
    // {
    //     var session = AuthStuff.GetSession(HttpContext.Items);
    //     if (session is null)
    //     {
    //         return Unauthorized();
    //     }
    //
    //     await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
    //     var status = await connection.QuerySingleOrDefaultAsync<ErodleStatus?>(
    //         @"select status from erodle_users where erodle_id = @erodleId and user_id = @userId",
    //         new { erodleId, userId = session.Player.Id });
    //     return status;
    // }

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
}
