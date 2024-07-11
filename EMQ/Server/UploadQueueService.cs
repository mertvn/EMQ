using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server;

public sealed class UploadQueueService : BackgroundService
{
    private readonly ILogger<UploadQueueService> _logger;

    private static List<Task> Tasks { get; } = new();

    public UploadQueueService(ILogger<UploadQueueService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UploadQueueService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWork();
            while (Tasks.Any(x => !x.IsCompleted))
            {
                await Task.WhenAny(Task.WhenAll(Tasks), Task.Delay(TimeSpan.FromSeconds(3), stoppingToken));
                await DoWork(); // allow new uploads to come through while we're waiting for encoding/transcoding
            }

            Tasks.Clear();
        }
    }

    private static async Task DoWork()
    {
        try
        {
            foreach ((string key, UploadQueueItem value) in ServerState.UploadQueue)
            {
                if (value.UploadResult.IsProcessing is null)
                {
                    value.UploadResult.IsProcessing = true;
                    var task = Task.Run(async () =>
                    {
                        UploadQueueItem updated = value;
                        try
                        {
                            updated = await DoWork_Inner(value);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error processing upload queue item: {e}");
                            updated.UploadResult.ErrorStr = "Unknown error when processing";
                        }

                        updated.UploadResult.IsProcessing = false;
                        ServerState.UploadQueue[key] = updated;
                    });
                    Tasks.Add(task);
                }
                else
                {
                    if ((DateTime.UtcNow - value.CreatedAt) > TimeSpan.FromHours(7))
                    {
                        if (value.UploadResult.IsProcessing.Value)
                        {
                            Console.WriteLine($"stuck upload? {key}");
                        }
                        else
                        {
                            while (ServerState.UploadQueue.ContainsKey(key))
                            {
                                ServerState.UploadQueue.TryRemove(key, out _);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static async Task<UploadQueueItem> DoWork_Inner(UploadQueueItem value)
    {
        int mId = value.Song.Id;
        Song song = value.Song;
        var file = value.MyFormFile;
        var uploadResult = value.UploadResult;
        var session = value.Session;
        var request = value.Request;

        int storageMode = 1; // 0: local disk, 1: SFTP
        const string outDir = @"M:\a\mb\selfhoststorage\pending"; // only used if storageMode == 0
        const long maxFileSize = UploadConstants.MaxFilesizeBytes;

        switch (file.Length)
        {
            case 0:
                uploadResult.ErrorStr = "File length is 0";
                return value;

            case > maxFileSize:
                uploadResult.ErrorStr = "File is too large";
                return value;
        }

        // todo check file signatures instead
        var mediaTypeInfo = UploadConstants.ValidMediaTypes.FirstOrDefault(x => x.MimeType == file.ContentType);
        if (mediaTypeInfo is null)
        {
            uploadResult.ErrorStr = "Invalid file format";
            return value;
        }

        string extension = mediaTypeInfo.Extension;
        string untrustedFileName = file.FileName;
        uploadResult.FileName = WebUtility.HtmlEncode(untrustedFileName);
        Console.WriteLine($"processing {uploadResult.FileName} by {session.Player.Username}");

        bool isBgm = song.Sources.Any(x => x.SongTypes.Contains(SongSourceSongType.BGM));
        FileStream? fs = null;
        string? tempPath = null;
        try
        {
            string guid = Guid.NewGuid().ToString();
            string trustedFileNameForFileStorage = $"{guid}.{extension}";
            tempPath = $"{Path.GetTempPath()}{trustedFileNameForFileStorage}";
            fs = new FileStream(tempPath, FileMode.Create);
            await file.FileStream.CopyToAsync(fs);
            fs.Position = 0;

            if (mediaTypeInfo.RequiresEncode)
            {
                await fs.DisposeAsync();
                string encodedPath;
                try
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(55));

                    uploadResult.ErrorStr = "Queued for encoding";
                    await MediaAnalyser.SemaphoreEncode.WaitAsync(cancellationTokenSource.Token);
                    try
                    {
                        uploadResult.ErrorStr = "Encoding...";
                        encodedPath =
                            await MediaAnalyser.EncodeIntoWebm(tempPath, 2, value.UploadOptions,
                                cancellationTokenSource.Token);
                    }
                    finally
                    {
                        MediaAnalyser.SemaphoreEncode.Release();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    uploadResult.ErrorStr = $"Error encoding: {e.Message}";
                    return value;
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }

                trustedFileNameForFileStorage = $"{guid}.webm";
                tempPath = encodedPath;
                fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
            }
            else if (mediaTypeInfo.RequiresTranscode)
            {
                await fs.DisposeAsync();
                string transcodedPath;
                try
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(25));

                    uploadResult.ErrorStr = "Queued for transcoding";
                    await MediaAnalyser.SemaphoreTranscode.WaitAsync(cancellationTokenSource.Token);
                    try
                    {
                        uploadResult.ErrorStr = "Transcoding...";
                        transcodedPath =
                            await MediaAnalyser.TranscodeInto192KMp3(tempPath,
                                value.UploadOptions, cancellationTokenSource.Token);
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
                    return value;
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }

                trustedFileNameForFileStorage = $"{guid}.mp3";
                tempPath = transcodedPath;
                fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
            }

            uploadResult.ErrorStr = "Storing the file...";
            string sha256 = CryptoUtils.Sha256Hash(fs);
            Console.WriteLine($"sha256:{sha256}");

            var dupesMel = await DbManager.FindMusicExternalLinkBySha256(sha256);
            var dupesRq = await DbManager.FindReviewQueueBySha256(sha256);
            if (dupesMel.Any() || dupesRq.Any()) // todo also dedup .weba
            {
                string dupeUrl = dupesMel.FirstOrDefault()?.url ?? dupesRq.First().url;
                Console.WriteLine($"dupe of {dupeUrl}");
                uploadResult.ResultUrl = dupeUrl.ReplaceSelfhostLink();
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

                    var resourcePath = new Uri($"{request.Scheme}://{request.Host}/");
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
                Url = uploadResult.ResultUrl.UnReplaceSelfhostLink(),
                Type = SongLinkType.Self,
                IsVideo = uploadResult.ResultUrl.IsVideoLink(),
                SubmittedBy = session.Player.Username,
                Sha256 = sha256,
            };

            await fs.DisposeAsync(); // needed to able to get the SHA256 during analysis
            var extractedAnalysis = await ServerUtils.ImportSongLinkInner(mId, songLink, tempPath, null);

            // todo cleanup
            if (songLink.IsVideo && extractedAnalysis != null)
            {
                uploadResult.ErrorStr = "Extracting audio...";
                switch (extractedAnalysis.PrimaryAudioStreamCodecName)
                {
                    case "opus":
                        {
                            string extractedOutputFinal = $"{Path.GetTempPath()}{guid}.weba";
                            Console.WriteLine(
                                $"extracting audio from video to {extractedOutputFinal}");

                            var process = new Process()
                            {
                                StartInfo = new ProcessStartInfo()
                                {
                                    FileName = "ffmpeg",
                                    Arguments =
                                        $"-i \"{tempPath}\" " +
                                        $"-map 0:a " +
                                        $"-c copy " +
                                        $"-f webm " +
                                        $"-nostdin " +
                                        $"\"{extractedOutputFinal}\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                }
                            };

                            process.Start();
                            process.BeginOutputReadLine();
                            string err = await process.StandardError.ReadToEndAsync();
                            if (err.Any())
                            {
                                Console.WriteLine(err.Last());
                            }

                            string extractedTrustedFileNameForFileStorage = $"{guid}.weba";
                            Console.WriteLine(Path.Combine(UploadConstants.SftpUserUploadDir,
                                "weba/",
                                extractedTrustedFileNameForFileStorage));

                            fs = new FileStream(extractedOutputFinal, FileMode.Open,
                                FileAccess.Read);
                            ServerUtils.SftpFileUpload(
                                UploadConstants.SftpHost, UploadConstants.SftpUsername,
                                UploadConstants.SftpPassword,
                                fs, Path.Combine(UploadConstants.SftpUserUploadDir, "weba/",
                                    extractedTrustedFileNameForFileStorage));

                            string extractedResultUrl =
                                $"https://emqselfhost/selfhoststorage/userup/weba/{extractedTrustedFileNameForFileStorage}"
                                    .ReplaceSelfhostLink();

                            uploadResult.ExtractedResultUrl = extractedResultUrl;
                            var songLinkExtracted = new SongLink
                            {
                                Url = extractedResultUrl.UnReplaceSelfhostLink(),
                                Type = SongLinkType.Self,
                                IsVideo = false,
                                SubmittedBy = session.Player.Username,
                                Sha256 = CryptoUtils.Sha256Hash(fs),
                            };

                            await fs.DisposeAsync();
                            await ServerUtils.ImportSongLinkInner(mId, songLinkExtracted,
                                extractedOutputFinal,
                                false);
                            break;
                        }
                    case "vorbis":
                        {
                            string extractedOutputFinal = $"{Path.GetTempPath()}{guid}.ogg";
                            Console.WriteLine(
                                $"extracting audio from video to {extractedOutputFinal}");

                            var process = new Process()
                            {
                                StartInfo = new ProcessStartInfo()
                                {
                                    FileName = "ffmpeg",
                                    Arguments =
                                        $"-i \"{tempPath}\" " +
                                        $"-map 0:a " +
                                        $"-c copy " +
                                        $"-nostdin " +
                                        $"\"{extractedOutputFinal}\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                }
                            };

                            process.Start();
                            process.BeginOutputReadLine();
                            string err = await process.StandardError.ReadToEndAsync();
                            if (err.Any())
                            {
                                Console.WriteLine(err.Last());
                            }

                            string extractedTrustedFileNameForFileStorage = $"{guid}.ogg";
                            Console.WriteLine(Path.Combine(UploadConstants.SftpUserUploadDir,
                                extractedTrustedFileNameForFileStorage));

                            fs = new FileStream(extractedOutputFinal, FileMode.Open,
                                FileAccess.Read);
                            ServerUtils.SftpFileUpload(
                                UploadConstants.SftpHost, UploadConstants.SftpUsername,
                                UploadConstants.SftpPassword,
                                fs, Path.Combine(UploadConstants.SftpUserUploadDir,
                                    extractedTrustedFileNameForFileStorage));

                            string extractedResultUrl =
                                $"https://emqselfhost/selfhoststorage/userup/{extractedTrustedFileNameForFileStorage}"
                                    .ReplaceSelfhostLink();

                            uploadResult.ExtractedResultUrl = extractedResultUrl;
                            var songLinkExtracted = new SongLink
                            {
                                Url = extractedResultUrl.UnReplaceSelfhostLink(),
                                Type = SongLinkType.Self,
                                IsVideo = false,
                                SubmittedBy = session.Player.Username,
                                Sha256 = CryptoUtils.Sha256Hash(fs),
                            };

                            await fs.DisposeAsync();
                            await ServerUtils.ImportSongLinkInner(mId, songLinkExtracted,
                                extractedOutputFinal,
                                false);
                            break;
                        }
                    case "mp3":
                        {
                            string extractedOutputFinal = $"{Path.GetTempPath()}{guid}.mp3";
                            Console.WriteLine(
                                $"extracting audio from video to {extractedOutputFinal}");

                            var process = new Process()
                            {
                                StartInfo = new ProcessStartInfo()
                                {
                                    FileName = "ffmpeg",
                                    Arguments =
                                        $"-i \"{tempPath}\" " +
                                        $"-map 0:a " +
                                        $"-c copy " +
                                        $"-nostdin " +
                                        $"\"{extractedOutputFinal}\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                }
                            };

                            process.Start();
                            process.BeginOutputReadLine();
                            string err = await process.StandardError.ReadToEndAsync();
                            if (err.Any())
                            {
                                Console.WriteLine(err.Last());
                            }

                            string extractedTrustedFileNameForFileStorage = $"{guid}.mp3";
                            Console.WriteLine(Path.Combine(UploadConstants.SftpUserUploadDir,
                                "weba/",
                                extractedTrustedFileNameForFileStorage));

                            fs = new FileStream(extractedOutputFinal, FileMode.Open,
                                FileAccess.Read);
                            ServerUtils.SftpFileUpload(
                                UploadConstants.SftpHost, UploadConstants.SftpUsername,
                                UploadConstants.SftpPassword,
                                fs, Path.Combine(UploadConstants.SftpUserUploadDir,
                                    extractedTrustedFileNameForFileStorage));

                            string extractedResultUrl =
                                $"https://emqselfhost/selfhoststorage/userup/{extractedTrustedFileNameForFileStorage}"
                                    .ReplaceSelfhostLink();

                            uploadResult.ExtractedResultUrl = extractedResultUrl;
                            var songLinkExtracted = new SongLink
                            {
                                Url = extractedResultUrl.UnReplaceSelfhostLink(),
                                Type = SongLinkType.Self,
                                IsVideo = false,
                                SubmittedBy = session.Player.Username,
                                Sha256 = CryptoUtils.Sha256Hash(fs),
                            };

                            await fs.DisposeAsync();
                            await ServerUtils.ImportSongLinkInner(mId, songLinkExtracted,
                                extractedOutputFinal,
                                false);
                            break;
                        }
                    default:
                        Console.WriteLine(
                            $"unsupported codec when extracting audio: {extractedAnalysis.PrimaryAudioStreamCodecName}");
                        break;
                }
            }
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

            await file.FileStream.DisposeAsync();
            if (System.IO.File.Exists(file.TempFsPath))
            {
                System.IO.File.Delete(file.TempFsPath);
            }
        }

        return value;
    }
}
