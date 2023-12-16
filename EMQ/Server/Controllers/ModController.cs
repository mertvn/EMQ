using System;
using System.IO;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpGet]
    [Route("ExportSongLite")]
    public async Task<ActionResult<string>> ExportSongLite()
    {
        string songLite = await DbManager.ExportSongLite();
        return songLite;
    }

    [CustomAuthorize(PermissionKind.Admin)]
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
        await ServerUtils.RunAnalysis();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ReviewSongLink)]
    [HttpPost]
    [Route("UpdateReviewQueueItem")]
    public async Task<ActionResult> UpdateReviewQueueItem([FromBody] ReqUpdateReviewQueueItem req)
    {
        await DbManager.UpdateReviewQueueItem(req.RQId, req.ReviewQueueStatus, reason: req.Notes, analyserResult: null);
        return Ok();
    }
}
