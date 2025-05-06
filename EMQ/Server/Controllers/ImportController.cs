using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Server.Db.Imports.EGS;
using EMQ.Server.Db.Imports.MusicBrainz;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.ImportHelper)]
[ApiController]
[Route("[controller]")]
public class ImportController : ControllerBase
{
    public ImportController(ILogger<ImportController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<ImportController> _logger;

    private static readonly SemaphoreSlim s_semaphoreImport = new(1);

    [CustomAuthorize(PermissionKind.ImportHelper)]
    [HttpGet]
    [Route("GetImporterPendingSongs")]
    public async Task<ActionResult<List<Song>>> GetImporterPendingSongs()
    {
        foreach (Song song in MusicBrainzImporter.PendingSongs)
        {
            if (song.MusicBrainzRecordingGid is not null)
            {
                if (DbManager.MusicBrainzRecordingReleases.TryGetValue(song.MusicBrainzRecordingGid.Value,
                        out var releases))
                {
                    song.MusicBrainzReleases = releases;
                }

                if (DbManager.MusicBrainzRecordingTracks.TryGetValue(song.MusicBrainzRecordingGid.Value,
                        out var tracks))
                {
                    song.MusicBrainzTracks = tracks;
                }
            }
        }

        return VndbImporter.PendingSongs.Concat(MusicBrainzImporter.PendingSongs).Take(300).ToList();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("SetPendingSongs")]
    public async Task<ActionResult> SetPendingSongs([FromForm] IEnumerable<IFormFile> files)
    {
        var file = files.Single();
        var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var json = (await JsonSerializer.DeserializeAsync<List<Song>>(ms, Utils.Jso))!;
        var vndb = json.Where(x => x.MusicBrainzRecordingGid is null || x.MusicBrainzRecordingGid == Guid.Empty)
            .ToList();
        var mb = json.Except(vndb).ToList();

        VndbImporter.PendingSongs = vndb;
        MusicBrainzImporter.PendingSongs = mb;
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunVndbImporter")]
    public async Task<ActionResult> RunVndbImporter()
    {
        var date = DateTime.UtcNow;
        date = DateTime.UtcNow - TimeSpan.FromDays(0); // todo
        await VndbImporter.ImportVndbData(date, true);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunEgsImporter")]
    public async Task<ActionResult> RunEgsImporter()
    {
        var date = DateTime.UtcNow - TimeSpan.FromDays(0);
        await EgsImporter.ImportEgsData(date, true);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunMusicBrainzImporter")]
    public async Task<ActionResult> RunMusicBrainzImporter()
    {
        await MusicBrainzImporter.ImportMusicBrainzData(true, true);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ImportHelper)]
    [HttpPost]
    [Route("InsertSong")]
    public async Task<ActionResult> InsertSong([FromBody] Song song)
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

        await s_semaphoreImport.WaitAsync();
        try
        {
            string newSongHash = song.ToSongLite().EMQSongHash;
            if (!VndbImporter.PendingSongs.Any(x => x.ToSongLite().EMQSongHash == newSongHash) &&
                !MusicBrainzImporter.PendingSongs.Any(x => x.ToSongLite().EMQSongHash == newSongHash))
            {
                return StatusCode(409);
            }

            int mId = await DbManager.InsertSong(song, isImport: true);
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

                await DbManager.EvictFromSongsCache(mId);
            }

            if (mId > 0)
            {
                Console.WriteLine($"{session.Player.Username} InsertSong {song}");
            }

            return mId > 0 ? Ok() : StatusCode(500);
        }
        finally
        {
            s_semaphoreImport.Release();
        }
    }

    [CustomAuthorize(PermissionKind.ImportHelper)]
    [HttpPost]
    [Route("OverwriteMusic")]
    public async Task<ActionResult> OverwriteMusic([FromBody] ReqOverwriteMusic req)
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

        await s_semaphoreImport.WaitAsync();
        try
        {
            string newSongHash = req.NewSong.ToSongLite().EMQSongHash;
            if (!VndbImporter.PendingSongs.Any(x => x.ToSongLite().EMQSongHash == newSongHash) &&
                !MusicBrainzImporter.PendingSongs.Any(x => x.ToSongLite().EMQSongHash == newSongHash))
            {
                return StatusCode(409);
            }

            bool success = await DbManager.OverwriteMusic(req.OldMid, req.NewSong, true);
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

                await DbManager.EvictFromSongsCache(req.OldMid);
            }

            if (success)
            {
                Console.WriteLine($"{session.Player.Username} OverwriteMusic {req.OldMid} => {req.NewSong}");
            }

            return success ? Ok() : StatusCode(500);
        }
        finally
        {
            s_semaphoreImport.Release();
        }
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RemoveFromPendingSongs")]
    public async Task<ActionResult> RemoveFromPendingSongs([FromBody] Song song)
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

        await s_semaphoreImport.WaitAsync();
        try
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

            return Ok();
        }
        finally
        {
            s_semaphoreImport.Release();
        }
    }
}
