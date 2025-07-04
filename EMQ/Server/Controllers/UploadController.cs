using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.User)]
[ApiController]
[Route("[controller]")]
public class UploadController : ControllerBase
{
    public UploadController(ILogger<UploadController> logger)
    {
        _logger = logger;
    }

    // ReSharper disable once NotAccessedField.Local
    private readonly ILogger<UploadController> _logger;

    [EnableRateLimiting(RateLimitKind.UploadFile)]
    [RequestSizeLimit(UploadConstants.MaxFilesizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadConstants.MaxFilesizeBytes)]
    [CustomAuthorize(PermissionKind.UploadSongLink)]
    [HttpPost]
    [Route("PostFile")]
    public async Task<ActionResult<UploadResult>> PostFile([FromForm] IEnumerable<IFormFile> files, [FromForm] int mId,
        [FromForm] string uploadOptionsStr)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        Session? session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        // todo
        // if (files.Count() > 1)
        // {
        //     uploadResult.ErrorStr = $"Only a single file is allowed per request";
        //     return new CreatedResult(uploadResult.ResultUrl ?? "", uploadResult);
        // }

        var song = (await DbManager.SelectSongsMIds(new[] { mId }, false)).SingleOrDefault();
        if (song == null)
        {
            return BadRequest("song is null");
        }

        var uploadOptions = JsonSerializer.Deserialize<UploadOptions>(uploadOptionsStr);
        if (uploadOptions == null)
        {
            return BadRequest("uploadOptions is null");
        }

        if (uploadOptions.To != 0 && uploadOptions.To < uploadOptions.Ss)
        {
            return BadRequest("Start time cannot be greater than End time");
        }

        if (song.Attributes.HasFlag(SongAttributes.Locked) &&
            !AuthStuff.HasPermission(session, PermissionKind.Moderator))
        {
            return BadRequest("Locked.");
        }

        var uploadResult = new UploadResult();
        var file = files.Single();
        string filename = WebUtility.HtmlEncode(file.FileName);
        string uploadId = $"{session.Player.Id};{mId};{file.Length};{filename}";
        while (!ServerState.UploadQueue.ContainsKey(uploadId))
        {
            string tempFsPath = $"{Path.GetTempPath()}{Guid.NewGuid().ToString()}";
            var fs = new FileStream(tempFsPath, FileMode.Create);
            await file.CopyToAsync(fs);
            fs.Position = 0;

            var myFormFile = new MyFormFile(file.Length, file.ContentType, filename, fs, tempFsPath);
            ServerState.UploadQueue.TryAdd(uploadId,
                new UploadQueueItem(uploadId, song, myFormFile, uploadResult, session, Request, uploadOptions));
        }

        uploadResult.UploadId = uploadId;
        uploadResult.ErrorStr = "Uploaded, processing...";
        uploadResult.FileName = file.FileName;
        uploadResult.ChosenMatch = song;
        return new CreatedResult(uploadResult.ResultUrl ?? "", uploadResult);
    }

    [CustomAuthorize(PermissionKind.User)]
    [HttpPost]
    [Route("GetUploadResult")]
    public async Task<ActionResult<UploadResult>> GetUploadResult([FromBody] string uploadId)
    {
        Session? session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        string[] split = uploadId.Split(';');
        if (split[0] != session.Player.Id.ToString())
        {
            return Unauthorized();
        }

        return ServerState.UploadQueue.TryGetValue(uploadId, out var uploadQueueItem)
            ? uploadQueueItem.UploadResult
            : StatusCode(404);
    }
}
