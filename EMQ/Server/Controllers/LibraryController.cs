﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using EMQ.Client.Components;
using EMQ.Client.Pages;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.Visitor)]
[ApiController]
[Route("[controller]")]
public class LibraryController : ControllerBase
{
    public LibraryController(ILogger<LibraryController> logger)
    {
        _logger = logger;
    }

    // ReSharper disable once NotAccessedField.Local
    private readonly ILogger<LibraryController> _logger;

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsBySongSourceTitle")]
    public async Task<IEnumerable<Song>> FindSongsBySongSourceTitle([FromBody] ReqFindSongsBySongSourceTitle req)
    {
        // todo
        int mId = int.TryParse(req.SongSourceTitle, out mId) ? mId : 0;
        if (mId > 0)
        {
            var songs = await DbManager.SelectSongsMIds(new[] { mId }, false);
            return songs;
        }
        else
        {
            var songs = await DbManager.FindSongsBySongSourceTitle(req.SongSourceTitle);
            return songs;
        }
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsBySongTitle")]
    public async Task<IEnumerable<Song>> FindSongsBySongTitle([FromBody] ReqFindSongsBySongTitle req)
    {
        var songs = await DbManager.FindSongsBySongTitle(req.SongTitle);
        return songs;
    }

    // todo this is actually unused right now
    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByArtistTitle")]
    public async Task<IEnumerable<Song>> FindSongsByArtistTitle([FromBody] ReqFindSongsByArtistTitle req)
    {
        var songs = await DbManager.FindSongsByArtistTitle(req.ArtistTitle);
        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByArtistId")]
    public async Task<IEnumerable<Song>> FindSongsByArtistId([FromBody] int artistId)
    {
        var songs = await DbManager.FindSongsByArtistId(artistId);
        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByUploader")]
    public async Task<IEnumerable<Song>> FindSongsByUploader([FromBody] string uploader)
    {
        var songs = await DbManager.FindSongsByUploader(uploader);
        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByYear")]
    public async Task<IEnumerable<Song>> FindSongsByYear([FromBody] ReqFindSongsByYear req)
    {
        var songs = await DbManager.FindSongsByYear(req.Year, req.Mode.ToSongSourceSongTypes());
        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByDifficulty")]
    public async Task<IEnumerable<Song>> FindSongsByDifficulty([FromBody] ReqFindSongsByDifficulty req)
    {
        var songs = await DbManager.FindSongsByDifficulty(req.Difficulty, req.Mode.ToSongSourceSongTypes());
        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByWarnings")]
    public async Task<IEnumerable<Song>> FindSongsByWarnings([FromBody] ReqFindSongsByWarnings req)
    {
        var songs = await DbManager.FindSongsByWarnings(req.Warnings, req.Mode.ToSongSourceSongTypes());
        return songs;
    }

    [Obsolete("deprecated in favor of Upload/PostFile")]
    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("ImportSongLink")]
    public async Task<bool> ImportSongLink([FromBody] ReqImportSongLink req)
    {
        if (ServerState.IsServerReadOnly || ServerState.IsSubmissionDisabled)
        {
            return false;
        }

        return await ServerUtils.ImportSongLinkInner(req.MId, req.SongLink, "", null) != null;
    }

    [CustomAuthorize(PermissionKind.ReportSongLink)]
    [HttpPost]
    [Route("SongReport")]
    public async Task<ActionResult> SongReport([FromBody] ReqSongReport req)
    {
        if (ServerState.IsServerReadOnly || ServerState.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        foreach ((string? url, bool _) in req.SelectedUrls.Where(x => x.Value))
        {
            req.SongReport.url = url;
            int _ = await DbManager.InsertSongReport(req.SongReport);
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindRQs")]
    public async Task<IEnumerable<RQ>> FindRQs([FromBody] ReqFindRQs req)
    {
        // var re = new ReqFindRQs(DateTime.UtcNow.AddDays(-14), DateTime.UtcNow.AddDays(1));
        // var r = await ServerUtils.Client.PostAsJsonAsync("https://erogemusicquiz.com/Library/FindRQs", re);
        // return await r.Content.ReadFromJsonAsync<List<RQ>>();

        var rqs = await DbManager.FindRQs(req.StartDate, req.EndDate);
        return rqs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindRQ")]
    public async Task<RQ> FindRQ([FromBody] int rqId)
    {
        var rq = await DbManager.FindRQ(rqId);
        return rq;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindEQs")]
    public async Task<IEnumerable<EditQueue>> FindEQs([FromBody] ReqFindRQs req)
    {
        var eqs = await DbManager.FindEQs(req.StartDate, req.EndDate);
        return eqs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindEQ")]
    public async Task<EditQueue> FindEQ([FromBody] int eqId)
    {
        var eq = await DbManager.FindEQ(eqId);
        return eq;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongReports")]
    public async Task<IEnumerable<SongReport>> FindRQs([FromBody] ReqFindSongReports req)
    {
        var songReports = await DbManager.FindSongReports(req.StartDate, req.EndDate);
        return songReports;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByLabels")]
    public async Task<IEnumerable<Song>> FindSongsByLabels([FromBody] ReqFindSongsByLabels req)
    {
        int[] mIds = await DbManager.FindMusicIdsByLabels(req.Labels, SongSourceSongTypeMode.Vocals);
        var songs = await DbManager.SelectSongsMIds(mIds, false);
        return songs.OrderBy(x => x.Id);
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpGet]
    [Route("GetLibraryStats")]
    public async Task<LibraryStats> GetLibraryStats([FromQuery] SongSourceSongTypeMode mode)
    {
        const int limit = 250;
        var libraryStats = await DbManager.SelectLibraryStats(limit, mode.ToSongSourceSongTypes());
        return libraryStats;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByVndbAdvSearchStr")]
    public async Task<IEnumerable<Song>> FindSongsByVndbAdvSearchStr([FromBody] string[] req)
    {
        List<Song> songs = new();

        string[] vndbUrls = req;
        foreach (string vndbUrl in vndbUrls)
        {
            var song = await DbManager.FindSongsByVndbUrl(vndbUrl);
            songs.AddRange(song);
        }

        return songs.DistinctBy(x => x.Id);
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByTitleAndArtistFuzzy")]
    public async Task<IEnumerable<Song>> FindSongsByTitleAndArtistFuzzy(
        [FromBody] ReqFindSongsByTitleAndArtistFuzzy req)
    {
        var songs = await DbManager.GetSongsByTitleAndArtistFuzzy(req.Titles, req.Artists,
            req.SongSourceSongTypeMode.ToSongSourceSongTypes());

        return songs;
    }

    [CustomAuthorize(PermissionKind.UploadSongLink)]
    [HttpPost]
    [Route("DeleteReviewQueueItem")]
    public async Task<ActionResult<bool>> DeleteReviewQueueItem([FromBody] int id)
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

        var rq = await DbManager.FindRQ(id);
        if (AuthStuff.HasPermission(session.UserRoleKind, PermissionKind.ReviewSongLink) ||
            string.Equals(rq.submitted_by, session.Player.Username, StringComparison.InvariantCultureIgnoreCase))
        {
            if (rq.status == ReviewQueueStatus.Pending)
            {
                Console.WriteLine($"{session.Player.Username} is deleting RQ {id} {rq.url} by {rq.submitted_by}");
                return await DbManager.DeleteEntity(new ReviewQueue { id = rq.id });
            }
            else
            {
                return Unauthorized();
            }
        }
        else
        {
            return Unauthorized();
        }
    }

    [CustomAuthorize(PermissionKind.UploadSongLink)]
    [HttpPost]
    [Route("DeleteEditQueueItem")]
    public async Task<ActionResult<bool>> DeleteEditQueueItem([FromBody] int id)
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

        var eq = await DbManager.FindEQ(id);
        if (AuthStuff.HasPermission(session.UserRoleKind, PermissionKind.ReviewSongLink) ||
            string.Equals(eq.submitted_by, session.Player.Username, StringComparison.InvariantCultureIgnoreCase))
        {
            if (eq.status == ReviewQueueStatus.Pending)
            {
                Console.WriteLine($"{session.Player.Username} is deleting EQ {id} by {eq.submitted_by}");
                return await DbManager.DeleteEntity(new EditQueue { id = eq.id });
            }
            else
            {
                return Unauthorized();
            }
        }
        else
        {
            return Unauthorized();
        }
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetSHSongStats")]
    public async Task<SHSongStats[]> GetSHSongStats([FromBody] int mId)
    {
        var res = await DbManager.GetSHSongStats(mId);
        return res;
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetLabelStats")]
    public async Task<ActionResult<LabelStats>> GetLabelStats([FromBody] string presetName)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        var vndbInfo = await ServerUtils.GetVndbInfo_Inner(session.Player.Id, presetName);
        if (string.IsNullOrWhiteSpace(vndbInfo.VndbId) || vndbInfo.Labels is null || !vndbInfo.Labels.Any())
        {
            return StatusCode(404);
        }

        int[] mIds = await DbManager.FindMusicIdsByLabels(vndbInfo.Labels, SongSourceSongTypeMode.Vocals);
        var res = await DbManager.GetLabelStats(mIds);
        return res;
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpGet]
    [Route("GetVNCoverUrl")]
    public async Task<ActionResult> GetVNCoverUrl([FromQuery] string id)
    {
        string? ret = null;
        const string sql = "SELECT image from vn where id = @id";
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb());
        string? screenshot = await connection.QueryFirstOrDefaultAsync<string?>(sql, new { id });
        if (!string.IsNullOrEmpty(screenshot))
        {
            (string modStr, int number) = Utils.ParseVndbScreenshotStr(screenshot);
            ret = $"https://emqselfhost/selfhoststorage/vndb-img/cv/{modStr}/{number}.jpg"
                .ReplaceSelfhostLink();
        }

        return ret != null ? File(await ServerUtils.Client.GetByteArrayAsync(ret), "image/jpg") : NotFound();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetSongSource")]
    public async Task<ActionResult<ResGetSongSource>> GetSongSource([FromBody] int id)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        var res = await DbManager.GetSongSource(id, session);
        return res;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetSongArtist")]
    public async Task<ActionResult<ResGetSongArtist>> GetSongArtist(ReqGetSongArtist req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        var res = await DbManager.GetSongArtist(req.AId, session);
        return res;
    }

    [CustomAuthorize(PermissionKind.UploadSongLink)] // todo
    [HttpPost]
    [Route("EditSong")]
    public async Task<ActionResult> EditSong([FromBody] ReqEditSong req)
    {
        if (ServerState.IsServerReadOnly || ServerState.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var comp = new EditSongComponent(); // todo move this method to somewhere better
        bool isValid = comp.ValidateSong(req.Song, req.IsNew);
        if (!isValid)
        {
            return BadRequest($"song object failed validation: {comp.ValidationMessages.First()}");
        }

        // todo important set unrequired stuff to null/empty for safety reasons
        // todo? extra validation for safety reasons

        req.Song.DataSource = DataSourceKind.EMQ;
        string? oldEntityJson = null;
        if (req.Song.Id <= 0)
        {
            int nextVal = await DbManager.SelectNextVal("public.music_id_seq");
            req.Song.Id = nextVal;
        }
        else
        {
            var song = (await DbManager.SelectSongsMIds(new[] { req.Song.Id }, false)).Single();
            oldEntityJson = JsonSerializer.Serialize(song, Utils.JsoCompact);
        }

        const int entityVersion = 1; // todo?
        const EntityKind entityKind = EntityKind.Song;
        var editQueue = new EditQueue
        {
            submitted_by = session.Player.Username,
            submitted_on = DateTime.UtcNow,
            status = ReviewQueueStatus.Pending,
            entity_kind = entityKind,
            entity_json = JsonSerializer.Serialize(req.Song, Utils.JsoCompact),
            entity_version = entityVersion,
            old_entity_json = oldEntityJson,
            note_user = req.NoteUser,
        };

        long eqId = await DbManager.InsertEntity(editQueue);
        if (eqId > 0)
        {
            Console.WriteLine($"{session.Player.Username} EditSong {req.Song}");
        }

        return eqId > 0 ? Ok() : StatusCode(500);
    }
}
