using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Dapper.Database.Extensions;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.MusicBrainz;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.Moderator)]
[ApiController]
[Route("[controller]")]
public class ModController : ControllerBase
{
    public ModController(ILogger<ModController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<ModController> _logger;

    [CustomAuthorize(PermissionKind.Moderator)]
    [HttpGet]
    [Route("ExportSongLite")]
    public async Task<ActionResult<string>> ExportSongLite()
    {
        string songLite = await DbManager.ExportSongLite();
        return songLite;
    }

    [CustomAuthorize(PermissionKind.Moderator)]
    [HttpGet]
    [Route("ExportSongLite_MB")]
    public async Task<ActionResult<string>> ExportSongLite_MB()
    {
        string songLite = await DbManager.ExportSongLite_MB();
        return songLite;
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunGc")]
    public async Task<ActionResult> RunGc()
    {
        ServerUtils.RunAggressiveGc();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunAnalysis")]
    public async Task<ActionResult> RunAnalysis()
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        await ServerUtils.RunAnalysis();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ReviewSongLink)]
    [HttpPost]
    [Route("UpdateReviewQueueItem")]
    public async Task<ActionResult> UpdateReviewQueueItem([FromBody] ReqUpdateReviewQueueItem req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        // todo use return value
        bool success = await DbManager.UpdateReviewQueueItem(req.RQId, req.ReviewQueueStatus, reason: req.Notes,
            analyserResult: null);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ReviewSongLink)] // todo perm
    [HttpPost]
    [Route("UpdateEditQueueItem")]
    public async Task<ActionResult> UpdateEditQueueItem([FromBody] ReqUpdateReviewQueueItem req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        bool success =
            await DbManager.UpdateEditQueueItem(transaction, req.RQId, req.ReviewQueueStatus, reason: req.Notes);
        if (success)
        {
            await transaction.CommitAsync();
            return Ok();
        }

        return StatusCode(409);
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("ToggleIsServerReadOnly")]
    public async Task<ActionResult> ToggleIsServerReadOnly()
    {
        ServerState.IsServerReadOnly = !ServerState.IsServerReadOnly;
        Console.WriteLine($"IsServerReadOnly: {ServerState.IsServerReadOnly}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("ToggleIsSubmissionDisabled")]
    public async Task<ActionResult> ToggleIsSubmissionDisabled()
    {
        ServerState.IsSubmissionDisabled = !ServerState.IsSubmissionDisabled;
        Console.WriteLine($"IsSubmissionDisabled: {ServerState.IsSubmissionDisabled}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("SetSubmittedBy")]
    public async Task<ActionResult> SetSubmittedBy([FromBody] ReqSetSubmittedBy req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        foreach (string url in req.Urls)
        {
            int rows = await DbManager.SetSubmittedBy(url.UnReplaceSelfhostLink(), req.SubmittedBy);
            if (rows > 0)
            {
                Console.WriteLine($"set {url} submitted_by to {req.SubmittedBy}");
            }
            else
            {
                Console.WriteLine($"failed setting {url} submitted_by to {req.SubmittedBy}");
                return StatusCode(500);
            }
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.DeleteSongLink)]
    [HttpPost]
    [Route("DeleteSongLink")]
    public async Task<ActionResult<int>> DeleteSongLink([FromBody] ReqDeleteSongLink req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        int rows = await DbManager.DeleteMusicExternalLink(req.MId, req.Url);
        rows += await DbManager.DeleteMusicExternalLink(req.MId, req.Url.UnReplaceSelfhostLink());
        if (rows > 0)
        {
            Console.WriteLine($"{session.Player.Username} DeleteSongLink {req.MId} {req.Url}");
        }

        return rows;
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("DeleteSong")]
    public async Task<ActionResult> DeleteSong([FromBody] int mId)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var music = await DbManager.GetEntity<Music>(mId);
        bool success = await DbManager.DeleteEntity(music!);
        if (success)
        {
            await DbManager.EvictFromSongsCache(mId);
        }

        if (success)
        {
            Console.WriteLine($"{session.Player.Username} DeleteSong {mId}");
        }

        return success ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("DeleteArtist")]
    public async Task<ActionResult> DeleteArtist([FromBody] int aId)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var artist = await DbManager.GetEntity<Artist>(aId);
        bool success = await DbManager.DeleteEntity(artist!);
        if (success)
        {
            // todo evict all songs with this artist
            await DbManager.EvictFromSongsCache(aId);
        }

        if (success)
        {
            Console.WriteLine($"{session.Player.Username} DeleteArtist {aId}");
        }

        return success ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Moderator)] // todo db mod requirement, eventually
    [HttpPost]
    [Route("SetSongAttributes")]
    public async Task<ActionResult> SetSongAttributes([FromBody] Song song)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        var music = (await DbManager.GetEntity<Music>(song.Id))!;
        Console.WriteLine(
            $"{session.Player.Username} is setting song attributes for mId {song.Id} {song} from {(SongAttributes)music.attributes} to {song.Attributes}");

        music.attributes = song.Attributes;
        bool success = await DbManager.UpdateEntity(music);
        if (success)
        {
            await DbManager.EvictFromSongsCache(song.Id);
        }

        return success ? Ok() : StatusCode(500);
    }
}
