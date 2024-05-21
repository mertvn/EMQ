using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;
using Npgsql;
using NUnit.Framework;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class EntryPoints_Encoding
{
    [Test, Explicit]
    public async Task FindAndEncodeVideos()
    {
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4", "ogv", "webm" };

        string inputDir = @"M:\!emqraw\!auto";
        // inputDir = @"N:\!checkedsorted";

        string baseOutputDir = @"M:\!emqvideos\!auto";

        var filePaths = new List<string>();
        foreach (string extension in searchForVideoExtensions)
        {
            filePaths.AddRange(Directory.EnumerateFiles(inputDir, $"*.{extension}",
                new EnumerationOptions { RecurseSubdirectories = true }));
        }

        foreach (string filePath in filePaths)
        {
            // todo? make this configurable
            // todo? just do full inputDir replace
            string oneUnderInputDir = filePath.Replace($"{inputDir}\\", "").Split("\\")[0];
            string outputDir = $"{baseOutputDir}\\{oneUnderInputDir}";
            string outputFinal = $"{outputDir}\\{Path.GetFileNameWithoutExtension(filePath)}.webm";

            try
            {
                if (File.Exists(outputFinal))
                {
                    Console.WriteLine($"skipping existing file {filePath}");
                    continue;
                }

                Console.WriteLine($"processing {filePath}");
                Directory.CreateDirectory(outputDir);
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(60));

                _ = await MediaAnalyser.EncodeIntoWebm(filePath, 4, cancellationTokenSource.Token, outputFinal);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                if (File.Exists(outputFinal))
                {
                    File.Delete(outputFinal);
                }

                if (Directory.Exists(outputDir) &&
                    Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories).Length == 0)
                {
                    Directory.Delete(outputDir);
                }
            }
        }
    }

    [Test, Explicit]
    public async Task SearchArchives()
    {
        string inputDir = @"N:\!checkedsorted";
        string[] files = Directory.GetFiles(inputDir, "*.rar", SearchOption.AllDirectories); // todo
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4" };
        // string[] searchForVideoStr = { "video", "movie", "mov", "mv" };

        string[] searchForXp3VideoStr = { "video", "movie", "mov", "mv", "op", "ed", "data" };

        string extractToDir = @"M:\!!temp";
        Directory.CreateDirectory(extractToDir);

        foreach (string file in files)
        {
            Console.WriteLine($"processing: {file}");
            try
            {
                var archive = ArchiveFactory.Open(file);
                foreach (IArchiveEntry entry in archive.Entries)
                {
                    if (!entry.Key.EndsWith("dll") &&
                        (searchForVideoExtensions.Any(x => entry.Key.EndsWith(x, StringComparison.OrdinalIgnoreCase))
                            // || searchForVideoStr.Any(x => entry.Key.Contains(x, StringComparison.OrdinalIgnoreCase))
                        ))
                    {
                        // Console.WriteLine(entry.Size);
                        if (entry.Size <= (7 * 1000 * 1000)) // 7 MB
                        {
                            Console.WriteLine($"skipping because of filesize: {entry.Key}");
                            continue;
                        }

                        Console.WriteLine($"extracting: {entry.Key}");

                        string finalDir = Path.Combine(extractToDir, new DirectoryInfo(file).Parent!.Name);
                        Directory.CreateDirectory(finalDir);

                        entry.WriteToDirectory(finalDir,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = false });
                    }

                    if (searchForXp3VideoStr.Any(x =>
                            entry.Key.EndsWith(".xp3", StringComparison.OrdinalIgnoreCase) &&
                            (entry.Key.EndsWith($"{x}.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}1.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}2.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}3.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}4.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}5.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}6.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}7.xp3", StringComparison.OrdinalIgnoreCase))))
                    {
                        Console.WriteLine($"XP3VideoStr: {entry.Key}");
                        string finalDir = Path.Combine(extractToDir, "!xp3", new DirectoryInfo(file).Parent!.Name);
                        // Console.WriteLine(finalDir);

                        Directory.CreateDirectory(finalDir);
                        entry.WriteToDirectory(finalDir,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = false });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing {file}: {e.Message}");
            }
        }
    }

    [Test, Explicit]
    public async Task GARbro_CLI()
    {
        string inputDir = @"M:\!!temp\!xp3";
        string[] files = Directory.GetFiles(inputDir, "*.xp3", SearchOption.AllDirectories); // todo

        string extractToDir = @"M:\!!temp\!garbrocli";
        Directory.CreateDirectory(extractToDir);

        foreach (string file in files)
        {
            Console.WriteLine($"processing {file}");
            string finalDir = Path.Combine(extractToDir, new DirectoryInfo(file).Parent!.Name);
            // Console.WriteLine(finalDir);

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = @"G:\Code\garbro-crskycode\GARbro\bin\Release\GARbro.Console.exe",
                    Arguments = $"x -ni -ns -na -y -o \"{finalDir}\" \"{file}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            string err = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (err.Any())
            {
                Console.WriteLine(err);
            }

            if (!Directory.Exists(finalDir))
            {
                // can enter here if no files could be extracted from the archive
                continue;
            }

            string[] filesFinal = Directory.GetFiles(finalDir, "*", SearchOption.AllDirectories);
            foreach (string fileFinal in filesFinal)
            {
                bool isValidVideoFile = true;
                try
                {
                    Console.WriteLine($"analyzing {fileFinal}");
                    IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(fileFinal);
                    // todo additional checks?
                    Console.WriteLine($"duration: {mediaInfo.Duration.TotalSeconds}s");
                    if (mediaInfo.Duration > TimeSpan.FromSeconds(3600))
                    {
                        Console.WriteLine("too long");
                        isValidVideoFile = false;
                    }
                    else if (mediaInfo.Duration > TimeSpan.FromSeconds(1) &&
                             mediaInfo.Duration < TimeSpan.FromSeconds(25))
                    {
                        Console.WriteLine("too short 1-25");
                        isValidVideoFile = false;
                    }
                    else if (mediaInfo.Duration <= TimeSpan.FromSeconds(1))
                    {
                        Console.WriteLine("too short <= 1");
                        isValidVideoFile = false;
                    }

                    if (fileFinal.Contains("demo", StringComparison.OrdinalIgnoreCase) || fileFinal.Contains("デモ"))
                    {
                        Console.WriteLine("demo");
                        isValidVideoFile = false;
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("Invalid data found when processing input") ||
                        e.Message.Contains("End of file") ||
                        e.Message.Contains("Invalid frame size") ||
                        e.Message.Contains("Not yet implemented") ||
                        e.Message.Contains("Failed to read frame"))
                    {
                        Console.WriteLine(e.Message);
                        isValidVideoFile = false;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (!isValidVideoFile)
                {
                    File.Delete(fileFinal);
                    if (Directory.GetFiles(finalDir, "*", SearchOption.AllDirectories).Length == 0)
                    {
                        Directory.Delete(finalDir, true);
                    }
                }
            }
        }
    }

    [Test, Explicit]
    public async Task ExtractAudioFromVideoForMissingSoundLinks()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
            Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
                .GroupBy(x => x.Item1)
                .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

            List<int> validMids = mids
                .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
                .Select(z => z.Key)
                .ToList();

            var songs = await DbManager.SelectSongsMIds(validMids, false);
            foreach (Song song in songs)
            {
                song.Links = SongLink.FilterSongLinks(song.Links);
            }

            var links = songs.Where(z => z.Links.Any() && !z.Links.Any(v => !v.IsVideo))
                .Select(x => x.Links.First(y => y.Type == SongLinkType.Self && y.IsVideo)).ToList();

            Dictionary<string, int> dict = new();
            foreach (Song song in songs)
            {
                foreach (SongLink songLink in song.Links)
                {
                    dict[songLink.Url] = song.Id;
                }
            }

            List<string> unsupported = new();
            foreach (SongLink songLink in links)
            {
                int mId = dict[songLink.Url];

                string tempPath = "M:/!!!temp/" + $"{songLink.Url.LastSegment()}";
                if (!File.Exists(tempPath))
                {
                    await ServerUtils.Client.DownloadFile(tempPath, new Uri(songLink.Url));
                }

                var extractedAnalysis = await MediaAnalyser.Analyse(tempPath, isVideoOverride: true);
                var session = new Session(new Player(-1, songLink.SubmittedBy!, new Avatar(AvatarCharacter.Auu)), "",
                    UserRoleKind.User, null);
                const string notes = "extracted from video";

                string guid = Guid.NewGuid().ToString();
                FileStream? fs;

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

                            var songLinkExtracted = new SongLink
                            {
                                Url = extractedResultUrl.UnReplaceSelfhostLink(),
                                Type = SongLinkType.Self,
                                IsVideo = false,
                                SubmittedBy = session.Player.Username,
                                Sha256 = CryptoUtils.Sha256Hash(fs),
                            };

                            await fs.DisposeAsync();
                            var res = await ServerUtils.ImportSongLinkInnerWithRQId(mId, songLinkExtracted,
                                extractedOutputFinal,
                                false);
                            await DbManager.UpdateReviewQueueItem(res.rqId, ReviewQueueStatus.Pending, reason: notes);
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

                            var songLinkExtracted = new SongLink
                            {
                                Url = extractedResultUrl.UnReplaceSelfhostLink(),
                                Type = SongLinkType.Self,
                                IsVideo = false,
                                SubmittedBy = session.Player.Username,
                                Sha256 = CryptoUtils.Sha256Hash(fs),
                            };

                            await fs.DisposeAsync();
                            var res = await ServerUtils.ImportSongLinkInnerWithRQId(mId, songLinkExtracted,
                                extractedOutputFinal,
                                false);
                            await DbManager.UpdateReviewQueueItem(res.rqId, ReviewQueueStatus.Pending, reason: notes);
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

                            var songLinkExtracted = new SongLink
                            {
                                Url = extractedResultUrl.UnReplaceSelfhostLink(),
                                Type = SongLinkType.Self,
                                IsVideo = false,
                                SubmittedBy = session.Player.Username,
                                Sha256 = CryptoUtils.Sha256Hash(fs),
                            };

                            await fs.DisposeAsync();
                            var res = await ServerUtils.ImportSongLinkInnerWithRQId(mId, songLinkExtracted,
                                extractedOutputFinal,
                                false);
                            await DbManager.UpdateReviewQueueItem(res.rqId, ReviewQueueStatus.Pending, reason: notes);
                            break;
                        }
                    default:
                        string str =
                            $"unsupported codec when extracting audio: {extractedAnalysis.PrimaryAudioStreamCodecName} {songLink.Url} {songs.First(x => x.Id == mId).ToString()}";
                        Console.WriteLine(str);
                        unsupported.Add(str);
                        break;
                }
            }

            foreach (string s in unsupported)
            {
                Console.WriteLine(s);
            }
        }
    }
}
