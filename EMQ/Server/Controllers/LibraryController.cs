using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Shared.Library;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
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
    public async Task<IEnumerable<Song>> FindSongsBySongSourceTitle(
        [FromBody] ReqFindSongsBySongSourceTitle req)
    {
        var songs = await DbManager.FindSongsBySongSourceTitle(req.SongSourceTitle);
        return songs;
    }

    [HttpPost]
    [Route("ImportSongLink")]
    public async Task<bool> ImportSongLink([FromBody] ReqImportSongLink req)
    {
        int melId = await DbManager.InsertSongLink(req.MId, req.SongLink);
        return melId > 0;
    }
}
