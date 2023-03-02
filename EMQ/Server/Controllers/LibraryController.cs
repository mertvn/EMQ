using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class LibraryController : ControllerBase
{
    public LibraryController(ILogger<LibraryController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<LibraryController> _logger;

    [HttpPost]
    [Route("FindSongsBySongSourceTitle")]
    public async Task<IEnumerable<Song>> FindSongsBySongSourceTitle([FromBody] ReqFindSongsBySongSourceTitle req)
    {
        var songs = await DbManager.FindSongsBySongSourceTitle(req.SongSourceTitle);
        return songs;
    }

    [HttpPost]
    [Route("FindSongsByArtistTitle")]
    public async Task<IEnumerable<Song>> FindSongsByArtistTitle([FromBody] ReqFindSongsByArtistTitle req)
    {
        var songs = await DbManager.FindSongsByArtistTitle(req.ArtistTitle);
        return songs;
    }

    [HttpPost]
    [Route("FindSongsByArtistId")]
    public async Task<IEnumerable<Song>> FindSongsByArtistId([FromBody] int artistId)
    {
        var songs = await DbManager.FindSongsByArtistId(artistId);
        return songs;
    }

    [HttpPost]
    [Route("ImportSongLink")]
    public async Task<bool> ImportSongLink([FromBody] ReqImportSongLink req)
    {
        int rqId = await DbManager.InsertReviewQueue(req.MId, req.SongLink, req.SubmittedBy, "Pending");

        if (rqId > 0)
        {
            string filePath = System.IO.Path.GetTempPath() + req.SongLink.Url.LastSegment();
            bool dlSuccess = await new HttpClient().DownloadFile(filePath, new Uri(req.SongLink.Url));
            if (dlSuccess)
            {
                var analyserResult = await MediaAnalyser.Analyse(filePath);

                string analyserResultStr;
                if (analyserResult.IsValid)
                {
                    analyserResultStr = "OK";
                }
                else
                {
                    analyserResultStr = string.Join(", ", analyserResult.Warnings.Select(x => x.ToString()));
                }

                await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending, analysis: analyserResultStr);
            }
        }

        return rqId > 0;
    }

    [HttpPost]
    [Route("FindRQs")]
    public async Task<IEnumerable<RQ>> FindRQs([FromBody] ReqFindRQs req)
    {
        var rqs = await DbManager.FindRQs(req.StartDate, req.EndDate);
        return rqs;
    }

    [HttpPost]
    [Route("FindSongsByLabels")]
    public async Task<IEnumerable<Song>> FindSongsByLabels([FromBody] ReqFindSongsByLabels req)
    {
        var songs = await DbManager.FindSongsByLabels(req.Labels);
        return songs;
    }

    [HttpGet]
    [Route("GetLibraryStats")]
    public async Task<LibraryStats> GetLibraryStats()
    {
        var libraryStats = await DbManager.SelectLibraryStats();
        return libraryStats;
    }

    [HttpPost]
    [Route("FindSongsByVndbAdvSearchStr")]
    public async Task<IEnumerable<Song>> FindSongsByVndbAdvSearchStr([FromBody] string[] req)
    {
        List<Song> songs = new();

        var vndbUrls = req;
        foreach (string vndbUrl in vndbUrls)
        {
            var song = await DbManager.FindSongsByVndbUrl(vndbUrl);
            songs.AddRange(song);
        }

        return songs;
    }
}
