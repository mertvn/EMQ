using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Client.Components;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Npgsql;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.Visitor)]
[ApiController]
[Route("[controller]")]
public class LibraryController : ControllerBase
{
    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByIds")]
    public async Task<IEnumerable<Song>> FindSongsByIds([FromBody] int[] mIds)
    {
        if (!mIds.Any())
        {
            return Array.Empty<Song>();
        }

        var songs = await DbManager.SelectSongsMIds(mIds, false);
        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsBySongSourceTitle")]
    public async Task<IEnumerable<Song>> FindSongsBySongSourceTitle([FromBody] ReqFindSongsBySongSourceTitle req)
    {
        IEnumerable<Song> songs = Array.Empty<Song>();
        if (req.SongSourceTitle.StartsWith("id:"))
        {
            if (int.TryParse(req.SongSourceTitle.Replace("id:", "").Trim(), out int msId))
            {
                var songsSource = await DbManager.GetSongSource(new SongSource() { Id = msId }, null, false);
                songs = await DbManager.FindSongsBySongSourceTitle(songsSource.SongSource.Titles.First().LatinTitle);
            }
        }
        else
        {
            songs = await DbManager.FindSongsBySongSourceTitle(req.SongSourceTitle);
        }

        return songs;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsBySongTitle")]
    public async Task<IEnumerable<Song>> FindSongsBySongTitle([FromBody] ReqFindSongsBySongTitle req)
    {
        IEnumerable<Song> songs = Array.Empty<Song>();
        if (req.SongTitle.StartsWith("id:"))
        {
            if (int.TryParse(req.SongTitle.Replace("id:", "").Trim(), out int mId))
            {
                songs = await DbManager.SelectSongsMIds(new[] { mId }, false);
            }
        }
        else
        {
            songs = await DbManager.FindSongsBySongTitle(req.SongTitle);
        }

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
        var songs =
            (await DbManager.FindSongsByWarnings(req.Warnings, req.Mode.ToSongSourceSongTypes()))
            .SelectMany(x => x.Value);
        return songs;
    }

    // [Obsolete("deprecated in favor of Upload/PostFile")]
    // [CustomAuthorize(PermissionKind.Admin)]
    // [HttpPost]
    // [Route("ImportSongLink")]
    // public async Task<bool> ImportSongLink([FromBody] ReqImportSongLink req)
    // {
    //     if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
    //     {
    //         return false;
    //     }
    //
    //     return await ServerUtils.ImportSongLinkInner(req.MId, req.SongLink, "", null) != null;
    // }

    [CustomAuthorize(PermissionKind.ReportSongLink)]
    [HttpPost]
    [Route("SongReport")]
    public async Task<ActionResult> SongReport([FromBody] ReqSongReport req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
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

    // todo IsShowAutomatedEdits
    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindRQs")]
    public async Task<List<RQ>> FindRQs([FromBody] ReqFindRQs req)
    {
        var rqs = await DbManager.FindRQs(req.StartDate, req.EndDate, req.SSST, req.Status);
        return rqs.Count > 7800 ? new List<RQ>() : rqs;
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
        var eqs = await DbManager.FindEQs(req.StartDate, req.EndDate, req.IsShowAutomatedEdits, req.Status);
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
    [Route("FindQueueItemsWithPendingChanges")]
    public async Task<ResFindQueueItemsWithPendingChanges> FindQueueItemsWithPendingChanges()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var pendingRQs =
            await connection.QueryAsync<(int, int)>(
                $"select distinct on (music_id) music_id, id from review_queue where status = {(int)ReviewQueueStatus.Pending}");

        var pendingEQs =
            await connection.QueryAsync<(int, int)>(
                $"select distinct on (entity_id) entity_id, id from edit_queue where status = {(int)ReviewQueueStatus.Pending}");
        return new ResFindQueueItemsWithPendingChanges
        {
            RQs = pendingRQs.ToDictionary(x => x.Item1, x => x.Item2),
            EQs = pendingEQs.ToDictionary(x => x.Item1, x => x.Item2),
        };
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongReports")]
    public async Task<IEnumerable<SongReport>> FindSongReports([FromBody] ReqFindSongReports req)
    {
        var songReports = await DbManager.FindSongReports(req.StartDate, req.EndDate);
        return songReports;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByLabels")]
    public async Task<IEnumerable<Song>> FindSongsByLabels([FromBody] ReqFindSongsByLabels req)
    {
        int[] mIds = await DbManager.FindMusicIdsByLabels(req.Labels, req.SSSTM);
        if (mIds.Length > 5200)
        {
            return Array.Empty<Song>();
        }

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

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByMBIDs")]
    public async Task<IEnumerable<Song>> FindSongsByMBIDs([FromBody] ReqFindSongsByMBIDs req)
    {
        var songs = await DbManager.GetSongsByMBIDs(req.MBIDs);
        return songs;
    }

    [CustomAuthorize(PermissionKind.UploadSongLink)]
    [HttpPost]
    [Route("DeleteReviewQueueItem")]
    public async Task<ActionResult<bool>> DeleteReviewQueueItem([FromBody] int id)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        var rq = await DbManager.FindRQ(id);
        if (AuthStuff.HasPermission(session, PermissionKind.ReviewSongLink) ||
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

    [CustomAuthorize(PermissionKind.Edit)]
    [HttpPost]
    [Route("DeleteEditQueueItem")]
    public async Task<ActionResult<bool>> DeleteEditQueueItem([FromBody] int id)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        var eq = await DbManager.FindEQ(id);
        if (AuthStuff.HasPermission(session, PermissionKind.ReviewEdit) ||
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
    public async Task<Dictionary<GuessKind, SHSongStats[]>> GetSHSongStats([FromBody] int mId)
    {
        var res = await DbManager.GetSHSongStats(mId, Constants.SHUseLastNPlaysPerPlayer);
        return res;
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetLabelStats")]
    public async Task<ActionResult<LabelStats>> GetLabelStats([FromBody] ReqGetLabelStats req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        var vndbInfo = await ServerUtils.GetVndbInfo_Inner(session.Player.Id, req.PresetName);
        if (string.IsNullOrWhiteSpace(vndbInfo.VndbId) || vndbInfo.Labels is null || !vndbInfo.Labels.Any())
        {
            return StatusCode(404);
        }

        int[] mIds = await DbManager.FindMusicIdsByLabels(vndbInfo.Labels, req.SSSTM);
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

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("GetCharsWithSimilarHairColor")]
    public async Task<ResGetCharsWithSimilarHairColor[]> GetCharsWithSimilarHairColor(
        ReqGetCharsWithSimilarHairColor req)
    {
        await ServerState.SemaphoreHair.WaitAsync(TimeSpan.FromMinutes(5));
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb());
            string? screenshot =
                (await connection.QueryAsync<string?>("SELECT c.image from chars c where c.id = @id",
                    new { id = req.TargetId })).FirstOrDefault();
            if (string.IsNullOrEmpty(screenshot))
            {
                return Array.Empty<ResGetCharsWithSimilarHairColor>();
            }

            (string modStr, int number) = Utils.ParseVndbScreenshotStr(screenshot);
            byte[] targetImgBytes = await ServerUtils.Client.GetByteArrayAsync(
                $"https://emqselfhost/selfhoststorage/vndb-img/ch/{modStr}/{number}.jpg".ReplaceSelfhostLink());
            string targetImgPath = Path.GetTempFileName();
            await System.IO.File.WriteAllBytesAsync(targetImgPath, targetImgBytes);

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = @"python",
                    Arguments = $"hair.py {targetImgPath} {req.TopN}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = "hair",
                }
            };
            process.Start();
            process.BeginErrorReadLine();
            string err = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (!err.Any())
            {
                return Array.Empty<ResGetCharsWithSimilarHairColor>();
            }

            // Console.WriteLine(err);
            string[] lines = err.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            var matches = new List<string>();
            foreach (string line in lines)
            {
                var match = RegexPatterns.HairRegex.Match(line);
                if (match.Success)
                {
                    matches.Add($"ch{match.Groups[2].Value}");
                }
            }

            var res =
                (await connection.QueryAsync<ResGetCharsWithSimilarHairColor>(
                    "SELECT c.id, c.name, c.latin, c.image from chars c where c.image = ANY(@matches)",
                    new { matches }))
                .Where(x => !req.ValidIds.Any() || req.ValidIds.Contains(x.Id)).Select(y =>
                {
                    (string modStr2, int number2) = Utils.ParseVndbScreenshotStr(y.Image);
                    return y with
                    {
                        ImageUrl = $"https://emqselfhost/selfhoststorage/vndb-img/ch/{modStr2}/{number2}.jpg"
                            .ReplaceSelfhostLink()
                    };
                }).ToArray();
            return res;
        }
        finally
        {
            ServerState.SemaphoreHair.Release();
        }
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetSongSource")]
    public async Task<ActionResult<ResGetSongSource>> GetSongSource(SongSource req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        var res = await DbManager.GetSongSource(req, session,
            req.LanguageOriginal == "gibstats"); // we don't talk about it
        return res;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetSongArtist")]
    public async Task<ActionResult<ResGetSongArtist>> GetSongArtist(SongArtist req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        var res = await DbManager.GetSongArtist(req, session, false);
        return res;
    }

    // the things a man does to avoid having to refactor the request object...
    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetSongArtistWithStats")]
    public async Task<ActionResult<ResGetSongArtist>> GetSongArtistWithStats(SongArtist req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        var res = await DbManager.GetSongArtist(req, session, true);
        return res;
    }

    [CustomAuthorize(PermissionKind.Edit)]
    [HttpPost]
    [Route("EditSong")]
    public async Task<ActionResult> EditSong([FromBody] ReqEditSong req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        foreach (var title in req.Song.Titles)
        {
            title.LatinTitle = title.LatinTitle.Trim();
            title.NonLatinTitle = title.NonLatinTitle?.Trim();
        }

        foreach (var link in req.Song.Links)
        {
            link.Url = link.Url.Trim();
        }

        var comp = new EditSongComponent(); // todo move this method to somewhere better
        bool isValid = comp.ValidateSong(req.Song, req.IsNew);
        if (!isValid)
        {
            return BadRequest($"song object failed validation: {comp.ValidationMessages.First()}");
        }

        // todo important set unrequired stuff to null/empty for safety reasons
        req.Song.Stats = null!;
        // todo? extra validation for safety reasons

        req.Song.Sort();
        string? oldEntityJson = null;
        if (req.Song.Id <= 0)
        {
            int nextVal = await DbManager.SelectNextVal("public.music_id_seq");
            req.Song.Id = nextVal;
        }
        else
        {
            var res = await DbManager.SelectSongsMIds(new[] { req.Song.Id }, false);
            Song song = res.Single();
            if (JsonNode.DeepEquals(JsonSerializer.SerializeToNode(song.Sort()),
                    JsonSerializer.SerializeToNode(req.Song)))
            {
                return BadRequest("No changes detected.");
            }

            if (song.Attributes.HasFlag(SongAttributes.Locked) &&
                !AuthStuff.HasPermission(session, PermissionKind.Moderator))
            {
                return BadRequest("Locked.");
            }

            song.Stats = null!;
            oldEntityJson = JsonSerializer.Serialize(song, Utils.JsoCompact);
        }

        const EntityKind entityKind = EntityKind.Song;
        var editQueue = new EditQueue
        {
            submitted_by = session.Player.Username,
            submitted_on = DateTime.UtcNow,
            status = ReviewQueueStatus.Pending,
            entity_kind = entityKind,
            entity_json = JsonSerializer.Serialize(req.Song, Utils.JsoCompact),
            entity_version = Constants.EntityVersionsDict[EntityKind.Song],
            old_entity_json = oldEntityJson,
            note_user = req.NoteUser,
            entity_id = req.Song.Id,
        };

        long eqId = await DbManager.InsertEntity(editQueue);
        if (eqId > 0)
        {
            Console.WriteLine($"{session.Player.Username} EditSong {req.Song}");
        }

        return eqId > 0 ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Edit)]
    [HttpPost]
    [Route("EditArtist")]
    public async Task<ActionResult> EditArtist([FromBody] ReqEditArtist req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        foreach (var title in req.Artist.Titles)
        {
            title.LatinTitle = title.LatinTitle.Trim();
            title.NonLatinTitle = title.NonLatinTitle?.Trim();
        }

        foreach (var link in req.Artist.Links)
        {
            link.Url = link.Url.Trim();
        }

        SongArtist? artist = null;
        if (!req.IsNew)
        {
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            var song = new Song { Artists = new List<SongArtist> { new() { Id = req.Artist.Id } } };
            var res = await DbManager.SelectArtistBatchNoAM(connection, new List<Song> { song }, false);
            artist = res.Single().Value.Single().Value;
        }

        var comp = new EditArtistComponent(); // todo move this method to somewhere better
        bool isValid = await comp.ValidateArtist(req.Artist, req.IsNew);
        if (!isValid)
        {
            return BadRequest($"artist object failed validation: {comp.ValidationMessages.First()}");
        }

        var content = await DbManager.GetSongArtist(
            new SongArtist
            {
                Links = req.Artist.Links.ExceptBy(
                        (artist?.Links.ToArray() ?? Array.Empty<SongArtistLink>()).Select(x => x.Url), x => x.Url)
                    .ToList()
            },
            session, false);
        if (content.SongArtists.Any())
        {
            return BadRequest(
                $"An artist linked to at least one of the external links you've added already exists in the database: ea{content.SongArtists.First().Id}");
        }

        // todo important set unrequired stuff to null/empty for safety reasons
        // todo? extra validation for safety reasons

        req.Artist.Sort();
        string? oldEntityJson = null;
        if (req.Artist.Id <= 0)
        {
            int nextVal = await DbManager.SelectNextVal("public.artist_id_seq");
            req.Artist.Id = nextVal;
        }
        else
        {
            if (JsonNode.DeepEquals(JsonSerializer.SerializeToNode(artist!.Sort()),
                    JsonSerializer.SerializeToNode(req.Artist)))
            {
                return BadRequest("No changes detected.");
            }

            oldEntityJson = JsonSerializer.Serialize(artist, Utils.JsoCompact);
        }

        const EntityKind entityKind = EntityKind.SongArtist;
        var editQueue = new EditQueue
        {
            submitted_by = session.Player.Username,
            submitted_on = DateTime.UtcNow,
            status = ReviewQueueStatus.Pending,
            entity_kind = entityKind,
            entity_json = JsonSerializer.Serialize(req.Artist, Utils.JsoCompact),
            entity_version = Constants.EntityVersionsDict[entityKind],
            old_entity_json = oldEntityJson,
            note_user = req.NoteUser,
            entity_id = req.Artist.Id,
        };

        long eqId = await DbManager.InsertEntity(editQueue);
        if (eqId > 0)
        {
            Console.WriteLine($"{session.Player.Username} EditArtist {req.Artist}");
            if (req.IsNew)
            {
                await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();

                int approvedEditCount = await connection.ExecuteScalarAsync<int>(
                    $"select count(*) from edit_queue where status = {(int)ReviewQueueStatus.Approved} and submitted_by = @username",
                    new { username = session.Player.Username });
                if (approvedEditCount > 50)
                {
                    Console.WriteLine($"automatically approving new artist {req.Artist} by {session.Player.Username}");
                    bool success = await DbManager.UpdateEditQueueItem(transaction, (int)eqId,
                        ReviewQueueStatus.Approved, "Automatically approved.");
                    if (success)
                    {
                        await transaction.CommitAsync();
                    }
                }
            }
        }

        return eqId > 0 ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Edit)]
    [HttpPost]
    [Route("EditDeleteSong")]
    public async Task<ActionResult> EditDeleteSong([FromBody] DeleteSong req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        const EntityKind entityKind = EntityKind.DeleteSong;
        var editQueue = new EditQueue
        {
            submitted_by = session.Player.Username,
            submitted_on = DateTime.UtcNow,
            status = ReviewQueueStatus.Pending,
            entity_kind = entityKind,
            entity_json = JsonSerializer.Serialize(req, Utils.JsoCompact),
            entity_version = Constants.EntityVersionsDict[entityKind],
            old_entity_json = null,
            note_user = req.NoteUser,
            entity_id = req.Id,
        };

        long eqId = await DbManager.InsertEntity(editQueue);
        if (eqId > 0)
        {
            Console.WriteLine($"{session.Player.Username} EditDeleteSong {req}");
        }

        return eqId > 0 ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Edit)]
    [HttpPost]
    [Route("EditMergeArtists")]
    public async Task<ActionResult> EditMergeArtists([FromBody] MergeArtists req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        const EntityKind entityKind = EntityKind.MergeArtists;
        var editQueue = new EditQueue
        {
            submitted_by = session.Player.Username,
            submitted_on = DateTime.UtcNow,
            status = ReviewQueueStatus.Pending,
            entity_kind = entityKind,
            entity_json = JsonSerializer.Serialize(req, Utils.JsoCompact),
            entity_version = Constants.EntityVersionsDict[EntityKind.MergeArtists],
            old_entity_json = null,
            note_user = "",
            entity_id = req.Id,
        };

        long eqId = await DbManager.InsertEntity(editQueue);
        if (eqId > 0)
        {
            Console.WriteLine($"{session.Player.Username} EditMergeArtists {req}");
        }

        return eqId > 0 ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Edit)]
    [HttpPost]
    [Route("EditSource")]
    public async Task<ActionResult> EditSource([FromBody] ReqEditSource req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        foreach (var title in req.Source.Titles)
        {
            title.LatinTitle = title.LatinTitle.Trim();
            title.NonLatinTitle = title.NonLatinTitle?.Trim();
        }

        foreach (var link in req.Source.Links)
        {
            link.Url = link.Url.Trim();
        }

        SongSource? source = null;
        if (!req.IsNew)
        {
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            var song = new Song { Sources = new List<SongSource> { new() { Id = req.Source.Id } } };
            var res = await DbManager.SelectSongSourceBatchNoMSM(connection, new List<Song> { song }, false);
            Dictionary<int, SongSource>? single = res.Single().Value;
            if (single != null)
            {
                source = single.Single().Value;
            }
        }

        var comp = new EditSourceComponent(); // todo move this method to somewhere better
        bool isValid = await comp.ValidateSource(req.Source, req.IsNew);
        if (!isValid)
        {
            return BadRequest($"source object failed validation: {comp.ValidationMessages.First()}");
        }

        var content = await DbManager.GetSongSource(
            new SongSource
            {
                Links = req.Source.Links.ExceptBy(
                        (source?.Links.ToArray() ?? Array.Empty<SongSourceLink>()).Select(x => x.Url), x => x.Url)
                    .ToList()
            },
            session, false);
        if (content.SongSource.Id > 0)
        {
            return BadRequest(
                $"A source linked to at least one of the external links you've added already exists in the database: ems{content.SongSource.Id}");
        }

        // todo important set unrequired stuff to null/empty for safety reasons
        // todo? extra validation for safety reasons

        req.Source.Sort();
        string? oldEntityJson = null;
        if (req.Source.Id <= 0)
        {
            int nextVal = await DbManager.SelectNextVal("public.music_source_id_seq");
            req.Source.Id = nextVal;
        }
        else
        {
            if (JsonNode.DeepEquals(JsonSerializer.SerializeToNode(source!.Sort()),
                    JsonSerializer.SerializeToNode(req.Source)))
            {
                return BadRequest("No changes detected.");
            }

            oldEntityJson = JsonSerializer.Serialize(source, Utils.JsoCompact);
        }

        const EntityKind entityKind = EntityKind.SongSource;
        var editQueue = new EditQueue
        {
            submitted_by = session.Player.Username,
            submitted_on = DateTime.UtcNow,
            status = ReviewQueueStatus.Pending,
            entity_kind = entityKind,
            entity_json = JsonSerializer.Serialize(req.Source, Utils.JsoCompact),
            entity_version = Constants.EntityVersionsDict[EntityKind.SongSource],
            old_entity_json = oldEntityJson,
            note_user = req.NoteUser,
            entity_id = req.Source.Id,
        };

        long eqId = await DbManager.InsertEntity(editQueue);
        if (eqId > 0)
        {
            Console.WriteLine($"{session.Player.Username} EditSource {req.Source}");
        }

        return eqId > 0 ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.UploadSongLink)]
    [HttpPost]
    [Route("EditSongLinkDetails")]
    public async Task<ActionResult> EditSongLinkDetails(ReqEditSongLinkDetails req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        string sh = Constants.SelfhostAddress!;
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        string submittedBy = (await connection.ExecuteScalarAsync<string>(
            "select submitted_by from music_external_link where music_id = @MId and REPLACE(url, @sh, 'https://emqselfhost') = @Url ",
            new { req.MId, sh, Url = req.SongLink.Url.UnReplaceSelfhostLink() }, transaction))!;
        RQ? rq = await connection.QueryFirstOrDefaultAsync<RQ>(
            "select * from review_queue where music_id = @MId and REPLACE(url, @sh, 'https://emqselfhost') = @Url ",
            new { req.MId, sh, Url = req.SongLink.Url.UnReplaceSelfhostLink() }, transaction);
        if (string.IsNullOrWhiteSpace(submittedBy))
        {
            submittedBy = rq?.submitted_by ?? "";
        }

        bool canEditDetails = AuthStuff.HasPermission(session, PermissionKind.ReviewSongLink) ||
                              string.Equals(submittedBy, session.Player.Username, StringComparison.OrdinalIgnoreCase);
        if (!canEditDetails)
        {
            return Unauthorized();
        }

        req.SongLink.Comment = req.SongLink.Comment.Trim();
        int rows = await connection.ExecuteAsync(
            "UPDATE music_external_link SET attributes = @Attributes, lineage = @Lineage, comment = @Comment where music_id = @MId and REPLACE(url, @sh, 'https://emqselfhost') = @Url",
            new
            {
                req.SongLink.Attributes,
                req.SongLink.Lineage,
                req.SongLink.Comment,
                req.MId,
                sh,
                Url = req.SongLink.Url.UnReplaceSelfhostLink()
            }, transaction);
        rows += await connection.ExecuteAsync(
            "UPDATE review_queue SET attributes = @Attributes, lineage = @Lineage, comment = @Comment where music_id = @MId and REPLACE(url, @sh, 'https://emqselfhost') = @Url",
            new
            {
                req.SongLink.Attributes,
                req.SongLink.Lineage,
                req.SongLink.Comment,
                req.MId,
                sh,
                Url = req.SongLink.Url.UnReplaceSelfhostLink()
            }, transaction);
        if (rows is <= 0 or >= 10)
        {
            return StatusCode(410);
        }

        await transaction.CommitAsync();
        if (req.SongLink.Lineage > SongLinkLineage.Unknown &&
            rq is { status: ReviewQueueStatus.Rejected } && rq.reason == ReviewQueueService.UnknownLineageRejectMessage)
        {
            await DbManager.UpdateReviewQueueItem(rq.id, ReviewQueueStatus.Pending, "-");
        }

        await DbManager.EvictFromSongsCache(req.MId);
        Console.WriteLine($"{session.Player.Username} EditSongLinkDetails {JsonSerializer.Serialize(req, Utils.Jso)}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("FindSongsByQuizSettings")]
    public async Task<ActionResult<List<Song>>> FindSongsByQuizSettings([FromBody] QuizSettings req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        req.SongSelectionKind = SongSelectionKind.Random;
        var room = new Room(Guid.Empty, "", session.Player) { Password = "", QuizSettings = req };
        room.Players.Enqueue(session.Player);
        var quiz = new Quiz(room, Guid.Empty);
        room.Quiz = quiz;
        var quizManager = new QuizManager(quiz);
        return await quizManager.PrimeQuiz() ? quiz.Songs.OrderBy(x => x.Id).ToList() : StatusCode(409);
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpGet]
    [Route("GetMBArtists")]
    [OutputCache(Duration = 15, PolicyName = "MyOutputCachePolicy")]
    public async Task<Dictionary<string, int>> GetMBArtists()
    {
        return await DbManager.GetMBArtists();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetEntityHistory")]
    public async Task<EditQueue[]> GetEntityHistory([FromBody] ReqGetEntityHistory req)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var res =
            await connection.QueryAsync<EditQueue>(
                $"select * from edit_queue where status = {(int)ReviewQueueStatus.Approved} and entity_kind = @entityKind and entity_id = @entityId order by id desc",
                new { entityKind = req.EntityKind, entityId = req.EntityId });
        return res.ToArray();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetMusicComments")]
    public async Task<ResGetMusicComments> GetMusicComments([FromBody] int musicId)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var musicComments =
            (await connection.QueryAsync<MusicComment>(
                $"select * from music_comment where music_id = @musicId order by id desc",
                new { musicId })).ToArray();

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var usernamesDict =
            (await connectionAuth.QueryAsync<(int, string)>(
                "select id, username from users where id = ANY(@userIds)",
                new { userIds = musicComments.Select(x => x.user_id).ToArray() }))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo cache this

        return new ResGetMusicComments { UsernamesDict = usernamesDict, MusicComments = musicComments, };
    }

    [CustomAuthorize(PermissionKind.Comment)]
    [HttpPost]
    [Route("InsertMusicComment")]
    public async Task<ActionResult> InsertMusicComment([FromBody] MusicComment req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        req.comment = req.comment.Trim();
        if (req.comment.Length is <= 0 or > 4096)
        {
            return StatusCode(409);
        }

        req.id = 0;
        req.user_id = session.Player.Id;
        req.created_at = DateTime.UtcNow;
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        bool success = await connection.InsertAsync(req);
        if (!success)
        {
            return StatusCode(520);
        }

        Console.WriteLine(
            $"p{session.Player.Id} {session.Player.Username} inserted music comment {req.music_id} = {req.comment}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Comment)]
    [HttpPost]
    [Route("DeleteMusicComment")]
    public async Task<ActionResult> DeleteMusicComment([FromBody] int id)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var mc = await DbManager.GetEntity<MusicComment>(id);
        if (mc is null)
        {
            return StatusCode(409);
        }

        if (!AuthStuff.HasPermission(session, PermissionKind.ReviewSongLink) && session.Player.Id != mc.user_id)
        {
            return Unauthorized();
        }

        Console.WriteLine(
            $"{session.Player.Username} is deleting MC {id} mId {mc.music_id} {mc.comment} by {mc.user_id}");
        bool success = await DbManager.DeleteEntity(new MusicComment() { id = mc.id });
        return success ? Ok() : StatusCode(520);
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetRecentMusicComments")]
    public async Task<ActionResult<ResGetRecentMusicComments>> GetRecentMusicComments()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var musicComments =
            (await connection.QueryAsync<MusicComment>("select * from music_comment order by id desc")).ToArray();

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var usernamesDict =
            (await connectionAuth.QueryAsync<(int, string)>(
                "select id, username from users where id = ANY(@userIds)",
                new { userIds = musicComments.Select(x => x.user_id).ToArray() }))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo cache this

        var songs = await DbManager.SelectSongsMIds(musicComments.Select(x => x.music_id).ToArray(), false);
        var songsDict = songs.ToDictionary(x => x.Id, x => x.ToStringLatin());

        return new ResGetRecentMusicComments()
        {
            ResGetMusicComments =
                new ResGetMusicComments { UsernamesDict = usernamesDict, MusicComments = musicComments },
            SongsDict = songsDict
        };
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpGet]
    [Route("GetUploadQueue")]
    [OutputCache(Duration = 15, PolicyName = "MyOutputCachePolicy")]
    public async Task<IEnumerable<string>> GetUploadQueue()
    {
        return ServerState.UploadQueue
            .Where(x => (x.Value.UploadResult.IsProcessing ?? true) || !x.Value.UploadResult.IsSuccess)
            .OrderBy(x => x.Value.CreatedAt)
            .Select(x =>
                $"{x.Value.CreatedAt:s} by {x.Value.Session.Player.Username} {x.Value.UploadResult.FileName} ({x.Value.Song.ToStringLatin()}) {x.Value.UploadResult.ErrorStr}");
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetUserCollections")]
    // [OutputCache(Duration = 15, PolicyName = "MyOutputCachePolicy")]
    public async Task<IEnumerable<int>> GetUserCollections([FromBody] int uid)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var collections = await connection.QueryAsync<int>(
            "select c.id from collection c join collection_users cu on cu.collection_id = c.id where cu.user_id = @uid",
            new { uid });
        return collections;
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetCollectionContainers")]
    // [OutputCache(Duration = 15, PolicyName = "MyOutputCachePolicy")]
    public async Task<ResGetCollectionContainers> GetCollectionContainers([FromBody] int[] cids)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

        // todo? fetch everything in a single query
        var collection =
            await connection.QueryAsync<Collection>("select * from collection where id = ANY(@cids)", new { cids });

        var collectionUsers =
            (await connection.QueryAsync<CollectionUsers>(
                "select * from collection_users where collection_id = ANY(@cids)",
                new { cids })).GroupBy(x => x.collection_id).ToDictionary(x => x.Key, x => x.ToList());

        var collectionEntities =
            (await connection.QueryAsync<CollectionEntity>(
                "select * from collection_entity where collection_id = ANY(@cids)",
                new { cids })).GroupBy(x => x.collection_id).ToDictionary(x => x.Key, x => x.ToList());

        var usernamesDict =
            (await connectionAuth.QueryAsync<(int, string)>(
                "select id, username from users where id = ANY(@userIds)",
                new { userIds = collectionUsers.SelectMany(x => x.Value.Select(y => y.user_id)).ToArray() }))
            .ToDictionary(x => x.Item1, x => x.Item2);

        var collectionContainers = collection.Select(x =>
            new CollectionContainer(x, collectionUsers.GetValueOrDefault(x.id) ?? new List<CollectionUsers>(),
                collectionEntities.GetValueOrDefault(x.id) ?? new List<CollectionEntity>()));
        return new ResGetCollectionContainers()
        {
            UsernamesDict = usernamesDict, CollectionContainers = collectionContainers.ToArray()
        };
    }

    [CustomAuthorize(PermissionKind.ManageCollections)]
    [HttpPost]
    [Route("UpsertCollection")]
    public async Task<ActionResult> UpsertCollection([FromBody] CollectionContainer req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        req.Collection.name = req.Collection.name.Trim();
        if (req.Collection.name.Length is <= 0 or > 128)
        {
            return StatusCode(409);
        }

        if (req.Collection.entity_kind is not EntityKind.Song)
        {
            return StatusCode(409);
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        bool success = true;
        if (req.Collection.id == 0)
        {
            req.Collection.created_at = DateTime.UtcNow;
            success &= await connection.InsertAsync(req.Collection, transaction);
            success &= await connection.InsertAsync(
                new CollectionUsers()
                {
                    collection_id = req.Collection.id,
                    user_id = session.Player.Id,
                    role = CollectionUsersRoleKind.Owner,
                }, transaction);
        }
        else
        {
            var owners = req.CollectionUsers.Where(x => x.role == CollectionUsersRoleKind.Owner).ToArray();
            if (owners.Length != 1 || owners.First().user_id != session.Player.Id) // todo? allow changing owner
            {
                return StatusCode(409);
            }

            if (req.CollectionUsers.Any(x => x.collection_id != req.Collection.id))
            {
                return StatusCode(409);
            }

            var collection = await connection.GetAsync<Collection>(req.Collection.id, transaction);
            if (collection == null)
            {
                return StatusCode(409);
            }

            bool isOwner = await connection.ExecuteScalarAsync<bool>(
                $"select 1 from collection_users where collection_id = @cid and user_id = @uid and role = {(int)CollectionUsersRoleKind.Owner}",
                new { cid = collection.id, uid = session.Player.Id },
                transaction);
            if (!isOwner)
            {
                return StatusCode(409);
            }

            success &= await connection.ExecuteAsync("UPDATE collection SET name = @name WHERE id = @id",
                new { req.Collection.id, req.Collection.name }, transaction) == 1;

            success &= await connection.ExecuteAsync("DELETE FROM collection_users WHERE collection_id = @id",
                new { req.Collection.id }, transaction) > 0;

            success &= await connection.InsertListAsync(
                req.CollectionUsers.Where(x => x.role > CollectionUsersRoleKind.None).ToArray(), transaction);
        }

        if (!success)
        {
            return StatusCode(520);
        }

        Console.WriteLine(
            $"p{session.Player.Id} {session.Player.Username} upserted {req.Collection.entity_kind} collection {req.Collection.name}");
        await transaction.CommitAsync();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ManageCollections)]
    [HttpPost]
    [Route("ModifyCollectionEntity")]
    public async Task<ActionResult> ModifyCollectionEntity([FromBody] ReqModifyCollectionEntity req)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var collection = await connection.GetAsync<Collection>(req.CollectionId, transaction);
        if (collection == null)
        {
            return StatusCode(409);
        }

        if (collection.entity_kind is not EntityKind.Song)
        {
            return StatusCode(409);
        }

        bool canEdit = await connection.ExecuteScalarAsync<bool>(
            $"select 1 from collection_users where collection_id = @cid and user_id = @uid and role >= {(int)CollectionUsersRoleKind.Editor}",
            new { cid = collection.id, uid = session.Player.Id },
            transaction);
        if (!canEdit)
        {
            return StatusCode(409);
        }

        var collectionEntity = new CollectionEntity
        {
            collection_id = collection.id,
            entity_id = req.EntityId, // todo? verify this exists
            modified_at = DateTime.UtcNow,
            modified_by = session.Player.Id,
        };

        bool success = true;
        if (req.IsAdded)
        {
            success &= await connection.InsertAsync(collectionEntity, transaction);
        }
        else
        {
            // not modifying success here because it could've been already removed
            await connection.DeleteAsync(collectionEntity, transaction);
        }

        if (!success)
        {
            return StatusCode(520);
        }

        Console.WriteLine(
            $"p{session.Player.Id} {session.Player.Username} ModifyCollectionEntity {collection.name} {collection.entity_kind} {req.EntityId} {req.IsAdded}");
        await transaction.CommitAsync();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ManageCollections)]
    [HttpPost]
    [Route("DeleteCollection")]
    public async Task<ActionResult> DeleteCollection([FromBody] int cid)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var collection = await connection.GetAsync<Collection>(cid, transaction);
        if (collection == null)
        {
            return StatusCode(409);
        }

        bool canDelete = await connection.ExecuteScalarAsync<bool>(
            $"select 1 from collection_users where collection_id = @cid and user_id = @uid and role = {(int)CollectionUsersRoleKind.Owner}",
            new { cid = collection.id, uid = session.Player.Id },
            transaction);
        if (!canDelete)
        {
            return StatusCode(409);
        }

        if (!await connection.DeleteAsync(collection, transaction))
        {
            return StatusCode(520);
        }

        Console.WriteLine(
            $"p{session.Player.Id} {session.Player.Username} DeleteCollection {collection.name} {collection.entity_kind}");
        await transaction.CommitAsync();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetEntityCollections")]
    public async Task<ActionResult<int[]>> GetEntityCollections([FromBody] CollectionContainer[] containers)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var entityKind = containers.First().Collection.entity_kind;
        if (containers.Any(x => x.Collection.entity_kind != entityKind))
        {
            return StatusCode(409);
        }

        var cids = await connection.QueryAsync<int>(
            "select id from collection c join collection_entity ce on c.id = ce.collection_id where entity_kind = @kind and entity_id = ANY(@eids)",
            new
            {
                kind = (int)entityKind,
                eids = containers.SelectMany(x => x.CollectionEntities.Select(y => y.entity_id)).ToArray()
            });
        return cids.ToArray();
    }

    [CustomAuthorize(PermissionKind.SearchLibrary)]
    [HttpPost]
    [Route("GetCollectionStats")]
    [OutputCache(Duration = 60, PolicyName = "MyOutputCachePolicy")]
    public async Task<List<CollectionStat>> GetCollectionStats()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

        var res = (await connection.QueryAsync<CollectionStat>(
            @$"SELECT c.id, c.name, cu.user_id as OwnerUserId, c.created_at as CreatedAt, COALESCE(MAX(ce.modified_at), c.created_at) AS ModifiedAt, COUNT(ce.entity_id) AS NumEntities
FROM collection c
LEFT JOIN collection_users cu ON c.id = cu.collection_id AND cu.role = {(int)CollectionUsersRoleKind.Owner}
JOIN collection_entity ce ON c.id = ce.collection_id
GROUP BY c.id, c.name, c.created_at, cu.user_id
ORDER BY c.id")).ToList();

        var usernamesDict =
            (await connectionAuth.QueryAsync<(int, string)>(
                "select id, username from users where id = ANY(@userIds)",
                new { userIds = res.Select(x => x.OwnerUserId).ToArray() }))
            .ToDictionary(x => x.Item1, x => x.Item2);
        foreach (CollectionStat r in res)
        {
            r.OwnerUsername = Utils.UserIdToUsername(usernamesDict, r.OwnerUserId);
        }

        return res;
    }
}
