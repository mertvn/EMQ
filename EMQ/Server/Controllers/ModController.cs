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

        await DbManager.UpdateReviewQueueItem(req.RQId, req.ReviewQueueStatus, reason: req.Notes, analyserResult: null);
        return Ok();
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

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("DeleteSongLink")]
    public async Task<ActionResult<int>> DeleteSongLink([FromBody] ReqDeleteSongLink req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        return await DbManager.DeleteMusicExternalLink(req.MId, req.Url.UnReplaceSelfhostLink());
    }

    [CustomAuthorize(PermissionKind.Moderator)]
    [HttpGet]
    [Route("GetImporterPendingSongs")]
    public async Task<ActionResult<List<Song>>> GetImporterPendingSongs()
    {
        foreach (Song song in MusicBrainzImporter.PendingSongs)
        {
            if (song.MusicBrainzRecordingGid is not null)
            {
                song.MusicBrainzReleases = DbManager.MusicBrainzRecordingReleases[song.MusicBrainzRecordingGid.Value];
            }
        }

        return VndbImporter.PendingSongs.Concat(MusicBrainzImporter.PendingSongs).ToList();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunVndbImporter")]
    public async Task<ActionResult> RunVndbImporter()
    {
        var date = DateTime.UtcNow;
        date = DateTime.UtcNow - TimeSpan.FromDays(1); // todo
        await VndbImporter.ImportVndbData(date, true);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunMusicBrainzImporter")]
    public async Task<ActionResult> RunMusicBrainzImporter()
    {
        var date = DateTime.UtcNow;
        date = DateTime.UtcNow - TimeSpan.FromDays(2); // todo
        await MusicBrainzImporter.ImportMusicBrainzData(true, true);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("InsertSong")]
    public async Task<ActionResult> InsertSong([FromBody] Song song)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        int mId = await DbManager.InsertSong(song);
        if (mId > 0)
        {
            if (song.MusicBrainzRecordingGid != null)
            {
                MusicBrainzImporter.PendingSongs.RemoveAll(x =>
                    x.ToSongLite_MB().Recording == song.ToSongLite_MB().Recording);
            }
            else
            {
                VndbImporter.PendingSongs.RemoveAll(x =>
                    x.ToSongLite().EMQSongHash == song.ToSongLite().EMQSongHash);
            }

            DbManager.EvictFromSongsCache(mId);
        }

        return mId > 0 ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("OverwriteMusic")]
    public async Task<ActionResult> OverwriteMusic([FromBody] ReqOverwriteMusic req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        bool success = await DbManager.OverwriteMusic(req.OldMid, req.NewSong);
        if (success)
        {
            if (req.NewSong.MusicBrainzRecordingGid != null)
            {
                MusicBrainzImporter.PendingSongs.RemoveAll(x =>
                    x.ToSongLite_MB().Recording == req.NewSong.ToSongLite_MB().Recording);
            }
            else
            {
                VndbImporter.PendingSongs.RemoveAll(x =>
                    x.ToSongLite().EMQSongHash == req.NewSong.ToSongLite().EMQSongHash);
            }

            DbManager.EvictFromSongsCache(req.OldMid);
        }

        return success ? Ok() : StatusCode(500);
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

        var music = await DbManager.GetEntity<Music>(mId);
        bool success = await DbManager.DeleteEntity(music!);
        if (success)
        {
            DbManager.EvictFromSongsCache(mId);
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

        music.attributes = (int)song.Attributes;
        bool success = await DbManager.UpdateEntity(music);
        if (success)
        {
            DbManager.EvictFromSongsCache(song.Id);
        }

        return success ? Ok() : StatusCode(500);
    }
}

// todo only hide spoilers if not finished/voted
