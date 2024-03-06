using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
            var songs = await DbManager.SelectSongs(new Song { Id = mId }, false);
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
        var songs = await DbManager.FindSongsByLabels(req.Labels, null);
        return songs;
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
}
