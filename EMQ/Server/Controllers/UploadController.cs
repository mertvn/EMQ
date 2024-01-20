using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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

[CustomAuthorize(PermissionKind.Moderator)]
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

        int filesProcessed = 0;
        List<UploadResult> uploadResults = new();
        foreach (var file in files)
        {
            var uploadResult = new UploadResult();

            // todo check file signatures instead
            var mediaTypeInfo = UploadConstants.ValidMediaTypes.FirstOrDefault(x => x.MimeType == file.ContentType);
            if (mediaTypeInfo is null)
            {
                uploadResult.ErrorStr = "Invalid file format";
                continue;
            }
            else if (mediaTypeInfo.RequiresEncode)
            {
                uploadResult.ErrorStr = "This file format requires encoding, which is not yet implemented";
                continue;
            }

            string extension = mediaTypeInfo.Extension;

            // this may fail but we don't really care
            string[] split = file.FileName.Split(";");
            int mId = Convert.ToInt32(split[0]);

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
                        string guid = Guid.NewGuid().ToString();
                        string trustedFileNameForFileStorage = $"{guid}.{extension}";
                        tempPath = $"{Path.GetTempPath()}{trustedFileNameForFileStorage}";
                        fs = new FileStream(tempPath, FileMode.Create);
                        await file.CopyToAsync(fs);
                        fs.Position = 0;

                        if (mediaTypeInfo.RequiresEncode)
                        {
                            throw new NotImplementedException();
                        }
                        else if (mediaTypeInfo.RequiresTranscode)
                        {
                            string transcodedPath;
                            try
                            {
                                var cancellationTokenSource = new CancellationTokenSource();
                                cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(25));

                                await MediaAnalyser.SemaphoreTranscode.WaitAsync(cancellationTokenSource.Token);
                                try
                                {
                                    transcodedPath = await MediaAnalyser.TranscodeInto192KMp3(tempPath);
                                }
                                finally
                                {
                                    MediaAnalyser.SemaphoreTranscode.Release();
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                uploadResult.ErrorStr = $"Error transcoding: {e.Message}";
                                continue;
                            }
                            finally
                            {
                                await fs.DisposeAsync();
                                if (System.IO.File.Exists(tempPath))
                                {
                                    System.IO.File.Delete(tempPath);
                                }
                            }

                            trustedFileNameForFileStorage = $"{guid}.mp3";
                            tempPath = transcodedPath;
                            fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
                        }

                        string sha256 = CryptoUtils.Sha256Hash(fs);
                        Console.WriteLine($"sha256:{sha256}");

                        var dupesMel = await DbManager.FindMusicExternalLinkBySha256(sha256);
                        var dupesRq = await DbManager.FindReviewQueueBySha256(sha256);
                        if (dupesMel.Any() || dupesRq.Any())
                        {
                            string dupeUrl = dupesMel.FirstOrDefault()?.url ?? dupesRq.First().url;
                            Console.WriteLine($"dupe of {dupeUrl}");
                            uploadResult.ResultUrl = dupeUrl;
                        }
                        else
                        {
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (storageMode == 0)
                            {
                                // have not tested if this storageMode works properly
                                Directory.CreateDirectory(outDir);
                                string newPath = Path.Combine(outDir, trustedFileNameForFileStorage);
                                System.IO.File.Copy(tempPath, newPath);

                                var resourcePath = new Uri($"{Request.Scheme}://{Request.Host}/");
                                uploadResult.ResultUrl =
                                    $"{resourcePath}selfhoststorage/userup/{trustedFileNameForFileStorage}";
                            }
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            else if (storageMode == 1)
                            {
                                // we need to use a FileStream here because it seems like FFProbe doesn't work when using a MemoryStream
                                ServerUtils.SftpFileUpload(
                                    UploadConstants.SftpHost, UploadConstants.SftpUsername,
                                    UploadConstants.SftpPassword,
                                    fs, Path.Combine(UploadConstants.SftpUserUploadDir, trustedFileNameForFileStorage));

                                uploadResult.ResultUrl =
                                    $"https://emqselfhost/selfhoststorage/userup/{trustedFileNameForFileStorage}"
                                        .ReplaceSelfhostLink();
                            }
                            else
                            {
                                throw new Exception("invalid storageMode");
                            }
                        }

                        uploadResult.Uploaded = true;
                        var songLink = new SongLink
                        {
                            Url = uploadResult.ResultUrl,
                            Type = SongLinkType.Self,
                            IsVideo = uploadResult.ResultUrl.IsVideoLink(),
                            SubmittedBy = session.Player.Username,
                            Sha256 = sha256,
                        };

                        await fs.DisposeAsync(); // needed to able to get the SHA256 during analysis
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
