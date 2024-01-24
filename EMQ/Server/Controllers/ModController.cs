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
            await DbManager.SetSubmittedBy(url, req.SubmittedBy);
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("DeleteSongLink")]
    public async Task<ActionResult> DeleteSongLink([FromBody] ReqDeleteSongLink req)
    {
        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        await DbManager.DeleteMusicExternalLink(req.MId, req.Url);
        return Ok();
    }
}
