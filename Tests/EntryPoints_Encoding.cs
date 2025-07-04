﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EMQ.Client;
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
using Session = EMQ.Shared.Auth.Entities.Concrete.Session;

namespace Tests;

// todo ps3
// ps3dec.exe --iso 1.iso --auto --tc 4

// todo psv
// pkg2zip.exe -x 1.pkg
// psvpfsparser.exe -i in -o out -f http://cma. -z zRifKey

// ReSharper disable once InconsistentNaming
public class EntryPoints_Encoding
{
    [Test, Explicit]
    public async Task FindAndEncodeVideos()
    {
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4", "ogv", "webm" };

        string inputDir = @"N:\!emqraw\!auto";
        // inputDir = @"N:\!checkedsorted";

        string baseOutputDir = @"N:\!emqvideos\!auto";

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

                UploadOptions uploadOptions = new()
                {
                    DoTwoPass = true, ShouldCropSilence = true, ShouldAdjustVolume = true
                };
                _ = await MediaAnalyser.EncodeIntoWebm(filePath, 4, uploadOptions, cancellationTokenSource.Token,
                    outputFinal);
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
    public async Task SearchArchivesExtractIsoFromRar()
    {
        string inputDir = @"N:\!checkedsorted\";
        string extractToDir = @"O:\!rarextract";
        Directory.CreateDirectory(extractToDir);

        var rarFiles = new List<string>();
        string[] rarExtensions = { "rar", "zip", "7z", };
        string[] isoExtensions = { "iso", "mdf", "img", "bin", "cdi" };
        foreach (string rarExtension in rarExtensions)
        {
            rarFiles.AddRange(Directory.EnumerateFiles(inputDir, $"*.{rarExtension}", SearchOption.AllDirectories));
        }

        const char startChar = 'K';
        const char endChar = 'L';

        rarFiles = rarFiles.Where(x =>
        {
            char firstChar = x.Replace(inputDir, "").First();
            return firstChar is >= startChar and <= endChar;
        }).ToList();

        foreach (string rarFile in rarFiles)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(600));

            Console.WriteLine($"processing {rarFile}");
            var process1 = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "7z",
                    Arguments = $"l \"{rarFile}\" -p ",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process1.Start();
            process1.BeginErrorReadLine();

            string err = await process1.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
            try
            {
                if (isoExtensions.Any(x => err.Contains(x, StringComparison.OrdinalIgnoreCase)))
                {
                    // string rarFilename = Path.GetFileNameWithoutExtension(rarFile);
                    var parent = new DirectoryInfo(rarFile).Parent;
                    // string finalDir = $"{extractToDir}/{parent!.Name}/{rarFilename}";
                    string finalDir = $"{extractToDir}/{parent!.Name}";
                    // if (Directory.Exists(finalDir))
                    // {
                    //     // Console.WriteLine($"skipping {finalDir}");
                    //     continue;
                    // }

                    Directory.CreateDirectory(finalDir);
                    var process2 = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "7z",
                            Arguments = $"x \"{rarFile}\" -r -y -p -o\"{finalDir}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process2.Start();
                    process2.BeginErrorReadLine();

                    string err2 = await process2.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
                    Console.WriteLine(err2);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing {rarFile}: {e.Message}");
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }
    }

    [Test, Explicit]
    public async Task SearchArchivesIso()
    {
        string inputDir = @"O:\!rarextract";
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4", "ogv", "mkv", "webm" };
        string extractToDir = @"O:\!!tempisofromrar";
        Directory.CreateDirectory(extractToDir);

        var isoFiles = new List<string>();
        string[] isoExtensions = { "iso", "mdf", "img", "bin", "cdi" };
        foreach (string isoExtension in isoExtensions)
        {
            isoFiles.AddRange(Directory.EnumerateFiles(inputDir, $"*.{isoExtension}", SearchOption.AllDirectories));
        }

        foreach (string isoFile in isoFiles)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(60));

            Console.WriteLine($"processing {isoFile}");
            var process1 = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "7z",
                    Arguments = $"l \"{isoFile}\" -p ",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process1.Start();
            process1.BeginErrorReadLine();

            string err = await process1.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
            try
            {
                if (searchForVideoExtensions.Any(x => err.Contains(x, StringComparison.OrdinalIgnoreCase)))
                {
                    // var foundExtensions = searchForVideoExtensions.Where(x=> entry.Contains(x));
                    // Console.WriteLine($"foundExtensions: {string.Join(",", foundExtensions)}");

                    var parent = new DirectoryInfo(isoFile).Parent;
                    // string isoFilename = Path.GetFileNameWithoutExtension(isoFile);
                    string finalDir = $"{extractToDir}/{parent!.Name}";
                    if (Directory.Exists(finalDir))
                    {
                        // Console.WriteLine($"skipping {finalDir}");
                        continue;
                    }

                    Directory.CreateDirectory(finalDir);
                    string extensionFilterStr = new(searchForVideoExtensions.SelectMany(x => $"\"*.{x}\" ").ToArray());
                    var process2 = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "7z",
                            Arguments = $"e \"{isoFile}\" -r -y -p -o\"{finalDir}\" {extensionFilterStr}",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process2.Start();
                    process2.BeginErrorReadLine();

                    string err2 = await process2.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
                    Console.WriteLine(err2);

                    string[] filesFinal = Directory.GetFiles(finalDir, "*", SearchOption.AllDirectories);
                    if (!filesFinal.Any())
                    {
                        Directory.Delete(finalDir, true);
                        continue;
                    }

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

                            // if (fileFinal.Contains("demo", StringComparison.OrdinalIgnoreCase) ||
                            //     fileFinal.Contains("デモ"))
                            // {
                            //     Console.WriteLine("demo");
                            //     isValidVideoFile = false;
                            // }
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
                else
                {
                    Console.WriteLine($"not found: {isoFile}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing {isoFile}: {e.Message}");
            }
            finally
            {
                cancellationTokenSource.Dispose();
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

                    // if (fileFinal.Contains("demo", StringComparison.OrdinalIgnoreCase) || fileFinal.Contains("デモ"))
                    // {
                    //     Console.WriteLine("demo");
                    //     isValidVideoFile = false;
                    // }
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

            var songs = await DbManager.SelectSongsMIds(validMids.ToArray(), false);
            foreach (Song song in songs)
            {
                song.Links = SongLink.FilterSongLinks(song.Links).Where(x => x.IsFileLink).ToList();
            }

            var links = songs.Where(z => z.Links.Any() && !z.Links.Any(v => !v.IsVideo))
                .Select(x => x.Links.First(y => y.Type == SongLinkType.Self && y.IsVideo)).ToList();

            var links2 = songs.Where(z =>
                    z.Links.Any(x => x.Url.EndsWith(".webm")) &&
                    !z.Links.Any(v => v.Url.EndsWith(".weba") || v.Url.EndsWith(".ogg")))
                .Select(x => x.Links.First(y => y.Type == SongLinkType.Self && y.IsVideo)).ToList();

            Dictionary<string, int> dict = new();
            foreach (Song song in songs)
            {
                foreach (SongLink songLink in song.Links)
                {
                    dict[songLink.Url] = song.Id;
                }
            }

            const string notes = "extracted from video";
            List<string> unsupported = new();
            HashSet<int> processedMids = new();
            foreach (SongLink songLink in links.Concat(links2))
            {
                int mId = dict[songLink.Url];
                if (!processedMids.Add(mId))
                {
                    continue;
                }

                string tempPath = "O:/!!!temp/" + $"{songLink.Url.LastSegment()}";
                if (!File.Exists(tempPath))
                {
                    await ServerUtils.Client.DownloadFile(tempPath, new Uri(songLink.Url));
                }

                var extractedAnalysis = await MediaAnalyser.Analyse(tempPath, isVideoOverride: true);
                var session = new Session(new Player(-1, songLink.SubmittedBy!, new Avatar(AvatarCharacter.Auu)), "",
                    UserRoleKind.User, null);

                string guid = songLink.Url.Contains("userup")
                    ? songLink.Url.LastSegment().Replace(".webm", "")
                    : Guid.NewGuid().ToString();
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
                            (_, int rqId) = await ServerUtils.ImportSongLinkInner(mId, songLinkExtracted,
                                extractedOutputFinal, false, null);
                            if (rqId > 0)
                            {
                                await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                                    reason: notes);
                            }

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
                            (_, int rqId) = await ServerUtils.ImportSongLinkInner(mId, songLinkExtracted,
                                extractedOutputFinal, false, null);
                            if (rqId > 0)
                            {
                                await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                                    reason: notes);
                            }

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
                            (_, int rqId) = await ServerUtils.ImportSongLinkInner(mId, songLinkExtracted,
                                extractedOutputFinal, false, null);
                            if (rqId > 0)
                            {
                                await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                                    reason: notes);
                            }

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

            var rqs = await DbManager.FindRQs(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1),
                SongSourceSongTypeMode.All, Enum.GetValues<ReviewQueueStatus>());
            var validRqs = rqs.Where(x => x.reason == notes);
            var processedMidsSongs = await DbManager.SelectSongsMIds(validRqs.Select(x => x.music_id).ToArray(), false);
            foreach (var processedMidsSong in processedMidsSongs)
            {
                var filtered = SongLink.FilterSongLinks(processedMidsSong.Links).Where(x => x.IsFileLink).ToArray();
                if (filtered.Any(x => x.Url.EndsWith(".weba")))
                {
                    foreach (var songLink in filtered.Where(x => !x.IsVideo && !x.Url.EndsWith(".weba")))
                    {
                        Console.WriteLine(JsonSerializer.Serialize(songLink, Utils.Jso));
                        await DbManager.DeleteMusicExternalLink(processedMidsSong.Id,
                            songLink.Url.UnReplaceSelfhostLink());
                    }
                }
                else if (filtered.Any(x => x.Url.EndsWith(".ogg")))
                {
                    foreach (var songLink in filtered.Where(x => !x.IsVideo && !x.Url.EndsWith(".ogg")))
                    {
                        Console.WriteLine(JsonSerializer.Serialize(songLink, Utils.Jso));
                        await DbManager.DeleteMusicExternalLink(processedMidsSong.Id,
                            songLink.Url.UnReplaceSelfhostLink());
                    }
                }
            }
        }
    }

    [Test, Explicit]
    public async Task FindCorruptVids()
    {
        int end = await DbManager.SelectCountUnsafe("music");
        var songs = await DbManager.SelectSongsMIds(Enumerable.Range(1, end).ToArray(), false);
        foreach (Song song in songs)
        {
            if (song.Id == 832)
            {
                continue;
            }

            var filtered = SongLink.FilterSongLinks(song.Links).Where(x => x.IsFileLink).ToArray();
            if (filtered.Any(x => x.Url.EndsWith(".weba") && x.SubmittedBy != "Lkyda") && !filtered.Any(x => x.IsVideo))
            {
                Console.WriteLine($"possibly corrupt link: {song}");
            }
        }
    }

    [Test, Explicit]
    public async Task FindValidSoundLinksFromMultipleUsers()
    {
        int end = await DbManager.SelectCountUnsafe("music");
        var songs = await DbManager.SelectSongsMIds(Enumerable.Range(1, end).ToArray(), false);
        foreach (Song song in songs)
        {
            var filtered = SongLink.FilterSongLinks(song.Links.Where(x => !x.IsVideo).ToList()).Where(x => x.IsFileLink)
                .ToArray();
            if (filtered.Any(x => x.SubmittedBy != filtered.First().SubmittedBy))
            {
                Console.WriteLine($"{song}");
            }
        }
    }

    [Test, Explicit]
    public async Task FindValidLinkCombinationFromMultipleUsers()
    {
        int end = await DbManager.SelectCountUnsafe("music");
        var songs = await DbManager.SelectSongsMIds(Enumerable.Range(1, end).ToArray(), false);
        foreach (Song song in songs)
        {
            var filtered = SongLink.FilterSongLinks(song.Links).Where(x => x.IsFileLink).ToArray();
            var validVideoLinks = filtered.Where(x => x.IsVideo).ToArray();
            var validSoundLinks = filtered.Where(x => !x.IsVideo).ToArray();
            string[] videoUploaders = validVideoLinks.Select(x => x.SubmittedBy ?? "").ToArray();
            string[] soundUploaders = validSoundLinks.Select(x => x.SubmittedBy ?? "").ToArray();

            // todo
            if (videoUploaders.Length != 1 || soundUploaders.Length != 1)
            {
                continue;
            }

            if (videoUploaders.First().NormalizeForAutocomplete() != soundUploaders.First().NormalizeForAutocomplete())
            {
                Console.WriteLine($"{song}");
            }
        }
    }

    [Test, Explicit]
    public async Task DoPSP_Step1_ExtractFromISO()
    {
        string isoDumpDir = @"N:\!emqraw\!psp\!isodump";
        string inputDir = @"";
        var isoFiles = Directory.EnumerateFiles(inputDir, $"*.iso", SearchOption.AllDirectories);
        foreach (string isoFile in isoFiles)
        {
            // Console.WriteLine($"processing {isoFile}");
            var process1 = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "7z",
                    Arguments = $"l \"{isoFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process1.Start();
            process1.BeginErrorReadLine();

            string err = await process1.StandardOutput.ReadToEndAsync();
            if (err.Contains(".pmf", StringComparison.OrdinalIgnoreCase))
            {
                string isoFilename = Path.GetFileNameWithoutExtension(isoFile);
                string finalDir = $"{isoDumpDir}/{isoFilename}";
                if (Directory.Exists(finalDir))
                {
                    // Console.WriteLine($"skipping {finalDir}");
                    continue;
                }

                Directory.CreateDirectory(finalDir);
                var process2 = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "7z",
                        Arguments = $"e \"{isoFile}\" -r -y -o\"{finalDir}\" \"*.pmf\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                process2.Start();
                process2.BeginErrorReadLine();

                string err2 = await process2.StandardOutput.ReadToEndAsync();
                Console.WriteLine(err2);
            }
            else if (err.Contains(".cpk", StringComparison.OrdinalIgnoreCase))
            {
                string isoFilename = Path.GetFileNameWithoutExtension(isoFile);
                string finalDir = $"{isoDumpDir}/{isoFilename}";
                if (Directory.Exists(finalDir))
                {
                    // Console.WriteLine($"skipping {finalDir}");
                    continue;
                }

                Directory.CreateDirectory(finalDir);
                var process2 = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "7z",
                        Arguments = $"e \"{isoFile}\" -r -y -o\"{finalDir}\" \"*.cpk\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                process2.Start();
                process2.BeginErrorReadLine();

                string err2 = await process2.StandardOutput.ReadToEndAsync();
                Console.WriteLine(err2);
            }
            else if (err.Contains(".cvm", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($".cvm support is not yet implemented: {isoFile}");
                // todo extract pmf from cvm with 7z
            }
            else
            {
                Console.WriteLine($"no valid files found in: {isoFile}");
            }
        }
    }

    class Platform
    {
        public string Name { get; set; } = "";

        public string Cpk { get; set; } = "";

        public string[] Pmf { get; set; } = Array.Empty<string>();

        public string[] Oma { get; set; } = Array.Empty<string>();

        public string[] Two64 { get; set; } = Array.Empty<string>();
    }

    [Test, Explicit]
    public async Task DoGeneric_Step2_Encode()
    {
        var platforms = new List<Platform>()
        {
            new()
            {
                Name = "psp",
                Cpk = "cpk",
                Pmf = new[] { "pmf" },
                Oma = new[] { "oma" }, // todo at3 for tamayura
                Two64 = new[] { "264" }
            },
            new()
            {
                Name = "ps3",
                Cpk = "cpk",
                Pmf = new[] { "pam", "usm" },
                Oma = new[] { "aa3", "adx", "genh" },
                Two64 = new[] { "264", "m2v" }
            },
            new()
            {
                Name = "ps2",
                Cpk = "cpk",
                Pmf = new[] { "pss" },
                Oma = new[] { "ss2" },
                Two64 = new[] { "m2v" }
            },
            new()
            {
                Name = "psvita",
                Cpk = "cpk",
                Pmf = new[] { "pam", "usm" },
                Oma = new[] { "aa3", "adx", "genh" },
                Two64 = new[] { "264", "m2v" }
            },
            new()
            {
                Name = "xbox",
                Cpk = "cpk",
                Pmf = new[] { "sfd", },
                Oma = new[] { "adx", },
                Two64 = new[] { "m2v", }
            },
        };

        foreach (Platform platform in platforms)
        {
            string inputDir = $@"N:\!emqraw\!{platform.Name}\!isodump";
            Directory.SetCurrentDirectory(inputDir);

            bool doCpk = !string.IsNullOrEmpty(platform.Cpk) && true;
            if (doCpk)
            {
                var cpkFiles = Directory.EnumerateFiles(inputDir, $"*.{platform.Cpk}", SearchOption.AllDirectories);
                foreach (string cpkFile in cpkFiles)
                {
                    if (Directory.Exists(cpkFile.Replace($".{platform.Cpk}", "", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Console.WriteLine($"skipping {cpkFile}");
                    }
                    else
                    {
                        Console.WriteLine($"processing {cpkFile}");
                        var process = new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                FileName = @"G:\jp\Programs\YACpkTool_v1.1\YACpkTool.exe",
                                Arguments = $"\"{cpkFile}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                RedirectStandardOutput = false,
                                RedirectStandardError = false,
                            }
                        };

                        process.Start();
                        await process.WaitForExitAsync();
                    }
                }
            }

            bool doPmf = true;
            if (doPmf)
            {
                foreach (string possiblePmf in platform.Pmf)
                {
                    var pmfFiles = Directory.EnumerateFiles(inputDir, $"*.{possiblePmf}", SearchOption.AllDirectories);
                    foreach (string pmfFile in pmfFiles)
                    {
                        bool alreadyExists = false;
                        var possibleOmaFiles = platform.Oma.SelectMany(x =>
                        {
                            var ret = new List<string>
                            {
                                // todo prop
                                pmfFile.Replace($".{possiblePmf}", $"_000001BD.{x}",
                                    StringComparison.OrdinalIgnoreCase),
                                pmfFile.Replace($".{possiblePmf}", $"_40534641.{x}",
                                    StringComparison.OrdinalIgnoreCase),
                                pmfFile.Replace($".{possiblePmf}", $"_400001BD.{x}",
                                    StringComparison.OrdinalIgnoreCase),
                                pmfFile.Replace($".{possiblePmf}", $"_A00001BD.{x}",
                                    StringComparison.OrdinalIgnoreCase),
                                pmfFile.Replace($".{possiblePmf}", $"_000001C0.{x}",
                                    StringComparison.OrdinalIgnoreCase)
                            };
                            return ret;
                        });

                        foreach (string possibleOmaFile in possibleOmaFiles)
                        {
                            if (File.Exists(possibleOmaFile))
                            {
                                // Console.WriteLine($"skipping {possibleOmaFile}");
                                alreadyExists = true;
                                break;
                            }
                        }

                        if (alreadyExists)
                        {
                            continue;
                        }

                        // foreach (string possibleOma in platform.Oma)
                        // {
                        // string omaFile = pmfFile.Replace($".{possiblePmf}", $"_000001BD.{possibleOma}",
                        //     StringComparison.OrdinalIgnoreCase);
                        // if (File.Exists(omaFile))
                        // {
                        //     // Console.WriteLine($"skipping {omaFile}");
                        // }
                        // else
                        // {

                        if (new FileInfo(pmfFile).Length == 0)
                        {
                            Console.WriteLine($"skipping 0 byte file: {pmfFile}");
                            continue;
                        }

                        Console.WriteLine($"processing {pmfFile}");
                        var process = new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                FileName =
                                    @"G:\Code\VGMToolbox\vgmtoolboxdemultiplexercli\bin\Debug\vgmtoolboxdemultiplexercli.exe",
                                Arguments = $"\"{pmfFile}\"",
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
                            Console.WriteLine(err);
                        }
                        // }
                        // }
                    }
                }
            }

            bool doOma = true;
            if (doOma)
            {
                foreach (string possibleOma in platform.Oma)
                {
                    var omaFiles = Directory.EnumerateFiles(inputDir, $"*.{possibleOma}", SearchOption.AllDirectories);
                    foreach (string omaFile in omaFiles)
                    {
                        string? videoFile = null;
                        foreach (string possibleTwo64 in platform.Two64)
                        {
                            string searchStr = $"BD.{possibleOma}";
                            bool contains = omaFile.Contains(searchStr);
                            string possibleVideoFile = omaFile.Replace(searchStr, $"E0.{possibleTwo64}");
                            if (contains && File.Exists(possibleVideoFile))
                            {
                                videoFile = possibleVideoFile;
                                break;
                            }
                            else
                            {
                                // idonteven
                                string searchStr2 = $"40534641.{possibleOma}";
                                bool contains2 = omaFile.Contains(searchStr2);
                                string possibleVideoFile2 = omaFile.Replace(searchStr2, $"40534656.{possibleTwo64}");
                                if (contains2 && File.Exists(possibleVideoFile2))
                                {
                                    videoFile = possibleVideoFile2;
                                    break;
                                }
                                else
                                {
                                    // idontevenpart2theelectricboogaloo
                                    string searchStr3 = $"400001BD.{possibleOma}";
                                    bool contains3 = omaFile.Contains(searchStr3);
                                    string possibleVideoFile3 =
                                        omaFile.Replace(searchStr3, $"000001E0.{possibleTwo64}");
                                    if (contains3 && File.Exists(possibleVideoFile3))
                                    {
                                        videoFile = possibleVideoFile3;
                                        break;
                                    }
                                    else
                                    {
                                        // ...
                                        string searchStr4 = $"A00001BD.{possibleOma}";
                                        bool contains4 = omaFile.Contains(searchStr4);
                                        string possibleVideoFile4 =
                                            omaFile.Replace(searchStr4, $"000001E0.{possibleTwo64}");
                                        if (contains4 && File.Exists(possibleVideoFile4))
                                        {
                                            videoFile = possibleVideoFile4;
                                            break;
                                        }
                                        else
                                        {
                                            // ....
                                            string searchStr5 = $"000001C0.{possibleOma}";
                                            bool contains5 = omaFile.Contains(searchStr5);
                                            string possibleVideoFile5 =
                                                omaFile.Replace(searchStr5, $"000001E0.{possibleTwo64}");
                                            if (contains5 && File.Exists(possibleVideoFile5))
                                            {
                                                videoFile = possibleVideoFile5;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (videoFile == null)
                        {
                            throw new Exception($".264 file not found for {omaFile}");
                        }

                        string flacFile = $"{omaFile}.flac";
                        if (File.Exists(flacFile))
                        {
                            // Console.WriteLine($"skipping {omaFile}");
                        }
                        else
                        {
                            Console.WriteLine($"processing {omaFile}");
                            {
                                var process = new Process()
                                {
                                    StartInfo = new ProcessStartInfo()
                                    {
                                        FileName = "ffmpeg",
                                        Arguments = $"-nostdin -i \"{omaFile}\" \"{flacFile}\"",
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
                                    Console.WriteLine(err);
                                }
                            }

                            {
                                var process = new Process()
                                {
                                    StartInfo = new ProcessStartInfo()
                                    {
                                        FileName = "ffmpeg",
                                        Arguments =
                                            $"-nostdin -i \"{videoFile}\" -i \"{flacFile}\" -c copy -map 0:v:0 -map 1:a:0 -strict -2 \"{videoFile}.mp4\"",
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
                                    Console.WriteLine(err);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Test, Explicit]
    public async Task DoPSP_Step2_Encode()
    {
        string inputDir = @"N:\!emqraw\!psp\!isodump";
        Directory.SetCurrentDirectory(inputDir);

        bool doCpk = true;
        if (doCpk)
        {
            var cpkFiles = Directory.EnumerateFiles(inputDir, $"*.cpk", SearchOption.AllDirectories);
            foreach (string cpkFile in cpkFiles)
            {
                if (Directory.Exists(cpkFile.Replace(".cpk", "", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"skipping {cpkFile}");
                }
                else
                {
                    Console.WriteLine($"processing {cpkFile}");
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = @"G:\jp\Programs\YACpkTool_v1.1\YACpkTool.exe",
                            Arguments = $"\"{cpkFile}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                }
            }
        }

        bool doPmf = true;
        if (doPmf)
        {
            var pmfFiles = Directory.EnumerateFiles(inputDir, $"*.pmf", SearchOption.AllDirectories);
            foreach (string pmfFile in pmfFiles)
            {
                string omaFile = pmfFile.Replace(".pmf", "_000001BD.oma", StringComparison.OrdinalIgnoreCase);
                if (File.Exists(omaFile))
                {
                    Console.WriteLine($"skipping {omaFile}");
                }
                else
                {
                    Console.WriteLine($"processing {pmfFile}");
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName =
                                @"G:\Code\VGMToolbox\vgmtoolboxdemultiplexercli\bin\Debug\vgmtoolboxdemultiplexercli.exe",
                            Arguments = $"\"{pmfFile}\"",
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
                        Console.WriteLine(err);
                    }
                }
            }
        }

        bool doOma = true;
        if (doOma)
        {
            var omaFiles = Directory.EnumerateFiles(inputDir, $"*.oma", SearchOption.AllDirectories);
            foreach (string omaFile in omaFiles)
            {
                string videoFile = omaFile.Replace("BD.oma", "E0.264");
                if (!File.Exists(videoFile))
                {
                    throw new Exception($".264 file not found at {videoFile}");
                }

                string flacFile = $"{omaFile}.flac";
                if (File.Exists(flacFile))
                {
                    Console.WriteLine($"skipping {omaFile}");
                }
                else
                {
                    Console.WriteLine($"processing {omaFile}");
                    {
                        var process = new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                FileName = "ffmpeg",
                                Arguments = $"-nostdin -i \"{omaFile}\" \"{flacFile}\"",
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
                            Console.WriteLine(err);
                        }
                    }

                    {
                        var process = new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                FileName = "ffmpeg",
                                Arguments =
                                    $"-nostdin -i \"{videoFile}\" -i \"{flacFile}\" -c copy -map 0:v:0 -map 1:a:0 -strict -2 \"{videoFile}.mp4\"",
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
                            Console.WriteLine(err);
                        }
                    }
                }
            }
        }
    }

    [Test, Explicit]
    public async Task RunAnalysis()
    {
        await ServerUtils.RunAnalysis();
    }

    [Test, Explicit]
    public async Task GenerateMissingSpectrograms()
    {
        const string baseDownloadDir = "K:/emq/emqsongsbackup2";
        Directory.SetCurrentDirectory(baseDownloadDir);
        string[] filePaths = Directory.GetFiles(baseDownloadDir, "*.*",
            new EnumerationOptions() { RecurseSubdirectories = true });

        var connectionInfo =
            new Renci.SshNet.ConnectionInfo(UploadConstants.SftpHost, UploadConstants.SftpUsername,
                new Renci.SshNet.PasswordAuthenticationMethod(UploadConstants.SftpUsername,
                    UploadConstants.SftpPassword));
        using var client = new Renci.SshNet.SftpClient(connectionInfo);
        client.Connect();
        string[] remoteFiles = client.ListDirectory($"{UploadConstants.SftpUserUploadDir}sox".Replace("\\", "/"))
            .Where(x => !x.FullName.EndsWith('.'))
            .Select(x => x.FullName[(x.FullName.LastIndexOf('/') + 1)..].Replace(".png", ""))
            .ToArray();
        client.Disconnect();

        foreach (string filePath in filePaths.Where(x => !remoteFiles.Contains(x.LastSegment())))
        {
            bool? isVideoOverride = null;
            if (filePath.EndsWith(".weba"))
            {
                isVideoOverride = false;
            }

            var res = await MediaAnalyser.Analyse(filePath, false, isVideoOverride, filePath.LastSegment());
            if (res.Warnings.Contains(MediaAnalyserWarningKind.UnknownError))
            {
                //throw new Exception();
            }
        }
    }
}
