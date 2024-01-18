using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Session = EMQ.Shared.Auth.Entities.Concrete.Session;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.Moderator)]
[ApiController]
[Route("[controller]")]
public class UploadController : ControllerBase
{
    public UploadController(ILogger<UploadController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<UploadController> _logger;

    [RequestSizeLimit(UploadConstants.MaxFilesizeBytes * UploadConstants.MaxFilesPerRequest)]
    [CustomAuthorize(PermissionKind.Moderator)]
    [HttpPost]
    [Route("PostFile")]
    public async Task<ActionResult<UploadResult>> PostFile([FromForm] IEnumerable<IFormFile> files)
    {
        if (ServerState.IsServerReadOnly || ServerState.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        Session? session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        int storageMode = 1; // 0: local disk, 1: SFTP
        const string outDir = @"M:\a\mb\selfhoststorage\pending"; // only used if storageMode == 0
        const int maxAllowedFiles = UploadConstants.MaxFilesPerRequest;
        const long maxFileSize = UploadConstants.MaxFilesizeBytes;

        string[] validExtensions = { "mp4", "webm", "mp3", "ogg" };

        var resourcePath = new Uri($"{Request.Scheme}://{Request.Host}/"); // todo?
        int filesProcessed = 0;
        List<UploadResult> uploadResults = new();
        foreach (var file in files)
        {
            var uploadResult = new UploadResult();

            // todo check file signatures instead
            // todo handle errors when splitting etc
            // todo validate file etc.
            string extension = file.FileName.Split(".").Last();
            string[] split = file.FileName.Split(";");
            int mId = Convert.ToInt32(split[0]); // todo

            string untrustedFileName = split[1];
            uploadResult.FileName = WebUtility.HtmlEncode(untrustedFileName);
            Console.WriteLine($"processing {uploadResult.FileName}");

            if (filesProcessed < maxAllowedFiles)
            {
                if (file.Length == 0)
                {
                    uploadResult.ErrorStr = "File length is 0";
                }
                else if (file.Length > maxFileSize)
                {
                    uploadResult.ErrorStr = "File is too large";
                }
                else
                {
                    FileStream? fs = null;
                    string? tempPath = null;
                    try
                    {
                        string trustedFileNameForFileStorage = $"{Guid.NewGuid()}.{extension}";
                        tempPath = $"{Path.GetTempPath()}{trustedFileNameForFileStorage}";
                        fs = new FileStream(tempPath, FileMode.Create);
                        await file.CopyToAsync(fs);
                        fs.Position = 0;

                        string hash = CryptoUtils.Sha256Hash(fs);
                        Console.WriteLine($"sha256:{hash}");
                        // todo prevent dupes (prob have to store hash in mel)

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (storageMode == 0)
                        {
                            // have not tested if this storageMode works properly
                            Directory.CreateDirectory(outDir);
                            string newPath = Path.Combine(outDir, trustedFileNameForFileStorage);
                            System.IO.File.Copy(tempPath, newPath);

                            uploadResult.ResultUrl =
                                $"{resourcePath}selfhoststorage/userup/{trustedFileNameForFileStorage}";
                        }
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        else if (storageMode == 1)
                        {
                            // we need to use a FileStream here because it seems like FFProbe doesn't work when using a MemoryStream
                            ServerUtils.SftpFileUpload(
                                UploadConstants.SftpHost, UploadConstants.SftpUsername, UploadConstants.SftpPassword,
                                fs, Path.Combine(UploadConstants.SftpUserUploadDir, trustedFileNameForFileStorage));

                            uploadResult.ResultUrl =
                                $"https://emqselfhost/selfhoststorage/userup/{trustedFileNameForFileStorage}"
                                    .ReplaceSelfhostLink();
                        }
                        else
                        {
                            throw new Exception("invalid storageMode");
                        }

                        uploadResult.Uploaded = true;
                        var songLink = new SongLink()
                        {
                            Url = uploadResult.ResultUrl,
                            Type = SongLinkType.Self,
                            IsVideo = uploadResult.ResultUrl.IsVideoLink(),
                            SubmittedBy = session.Player.Username,
                        };

                        await ServerUtils.ImportSongLinkInner(mId, songLink, tempPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        uploadResult.ErrorStr = $"Error uploading: {ex.Message}";
                    }
                    finally
                    {
                        if (fs != null)
                        {
                            await fs.DisposeAsync();
                        }

                        if (tempPath != null && System.IO.File.Exists(tempPath))
                        {
                            System.IO.File.Delete(tempPath);
                        }
                    }
                }

                filesProcessed++;
            }
            else
            {
                uploadResult.ErrorStr = $"Only {maxAllowedFiles} files is allowed per request";
            }

            uploadResults.Add(uploadResult);
        }

        UploadResult single = uploadResults.Single();
        return new CreatedResult(single.ResultUrl!, single);
    }
}
