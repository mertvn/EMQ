using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.SongMatching;
using EMQ.Server.Db.Imports.SongMatching.Common;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;
using FFMpegCore.Enums;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class EntryPoints_SongMatching
{
    [Test, Explicit]
    public async Task ImportGGVC()
    {
        await GGVCImporter.ImportGGVC();
    }

    [Test, Explicit]
    public async Task DeleteAlreadyImportedGGVCFiles()
    {
        await GGVCImporter.DeleteAlreadyImportedGGVCFiles();
    }

    [Test, Explicit]
    public async Task UploadMatchedSongs()
    {
        string root = "C:\\emq\\matching\\generic\\";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            var songMatchInnerResults =
                JsonSerializer.Deserialize<List<SongMatchInnerResult>>(
                    await File.ReadAllTextAsync($"{dir}\\matched.json"),
                    Utils.JsoIndented)!;

            if (!File.Exists($"{dir}\\uploaded.json"))
            {
                await File.WriteAllTextAsync($"{dir}\\uploaded.json", "[]");
            }

            var uploaded =
                JsonSerializer.Deserialize<List<Uploadable>>(
                    await File.ReadAllTextAsync($"{dir}\\uploaded.json"),
                    Utils.JsoIndented)!;

            int oldCount = uploaded.Count;
            var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();

            for (int index = 0; index < songMatchInnerResults.Count; index++)
            {
                SongMatchInnerResult songMatchInnerResult = songMatchInnerResults[index];
                var uploadable = new Uploadable
                {
                    Path = songMatchInnerResult.SongMatch.Path, MId = songMatchInnerResult.mIds.Single()
                };

                if (uploaded.Any(x => x.Path == songMatchInnerResult.SongMatch.Path))
                {
                    continue;
                }

                if (midsWithSoundLinks.Any(x => x == uploadable.MId))
                {
                    if (uploaded.Count > oldCount)
                    {
                        Console.WriteLine("Skipping uploadable with existing mId: " +
                                          JsonSerializer.Serialize(uploadable, Utils.Jso));
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(uploadable.Path))
                {
                    Console.WriteLine("path is null: " + JsonSerializer.Serialize(uploadable, Utils.JsoIndented));
                    continue;
                }

                // if (uploaded.Count - oldCount >= 100)
                // {
                //     break;
                // }

                if (!uploadable.Path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    // Console.WriteLine("skipping non .mp3: " + uploadable.Path);
                    // continue;

                    string newPath = uploadable.Path.Replace(Path.GetExtension(uploadable.Path), ".mp3");
                    if (!File.Exists(newPath))
                    {
                        Console.WriteLine("converting non .mp3: " + uploadable.Path);
                        FFMpegArguments
                            .FromFileInput(uploadable.Path)
                            .OutputToFile(newPath, false, options => options
                                .WithAudioCodec(AudioCodec.LibMp3Lame)
                                .WithAudioBitrate(192)
                                .WithTagVersion())
                            .ProcessSynchronously();
                    }

                    if (uploaded.Any(x => x.Path == newPath))
                    {
                        continue;
                    }

                    uploadable.Path = newPath;
                }

                string catboxUrl = await CatboxUploader.Upload(uploadable);
                uploadable.ResultUrl = catboxUrl;
                Console.WriteLine(catboxUrl);
                if (!catboxUrl.EndsWith(".mp3"))
                {
                    Console.WriteLine("invalid resultUrl: " + JsonSerializer.Serialize(uploadable, Utils.JsoIndented));
                    continue;
                }

                var songLite = (await DbManager.SelectSongs(new Song { Id = uploadable.MId }, false)).Single()
                    .ToSongLite();
                uploadable.SongLite = songLite;
                uploaded.Add(uploadable);

                await File.WriteAllTextAsync($"{dir}\\uploaded.json",
                    JsonSerializer.Serialize(uploaded, Utils.JsoIndented));

                await Task.Delay(10000);
            }

            if (uploaded.Count - oldCount > 0)
            {
                await File.WriteAllTextAsync($"{dir}\\uploaded_backup_{DateTime.UtcNow:yyyyMMddTHHmmss}.json",
                    JsonSerializer.Serialize(uploaded, Utils.JsoIndented));
            }

            Console.WriteLine($"Uploaded {uploaded.Count - oldCount} files in {dir}.");
        }
    }

    [Test, Explicit]
    public async Task SetSubmittedByToRobotName()
    {
        const string submittedBy = Constants.RobotName;
        int totalRows = 0;

        string root = "C:\\emq\\matching";
        string[] filePaths = Directory.GetFiles(root, "uploaded.json", SearchOption.AllDirectories);
        foreach (string filePath in filePaths)
        {
            var uploaded =
                JsonSerializer.Deserialize<List<Uploadable>>(
                    await File.ReadAllTextAsync(filePath),
                    Utils.JsoIndented)!;

            foreach (Uploadable uploadable in uploaded)
            {
                if (!uploadable.ResultUrl?.Contains("catbox") ?? true)
                {
                    continue;
                }

                int rows = await DbManager.SetSubmittedBy(uploadable.ResultUrl!, submittedBy);
                if (rows > 0)
                {
                    totalRows += rows;
                    Console.WriteLine(
                        $"set {uploadable.SongLite.SourceVndbIds.First()} {uploadable.ResultUrl} submitted_by to {submittedBy}");
                }
                else
                {
                    Console.WriteLine(
                        $"failed setting {uploadable.SongLite.SourceVndbIds.First()} {uploadable.ResultUrl} submitted_by to {submittedBy}");
                }
            }
        }

        Console.WriteLine($"totalRows: {totalRows}");
    }

    [Test, Explicit]
    public async Task SetSubmittedByFromFile()
    {
        const string submittedBy = "Burnal";

        string filePath = @"C:\emq\other\Yeni_Metin_Belgesi.txt";
        string[] contents = await File.ReadAllLinesAsync(filePath);
        foreach (string content in contents)
        {
            var match = Regex.Match(content, @"(https:\/\/files.catbox.moe\/.+\.mp4)");
            string url = match.Groups[1].Value;
            int rows = await DbManager.SetSubmittedBy(url, submittedBy);
        }
    }

    [Test, Explicit]
    public async Task SetSubmittedByFromReviewQueueJson()
    {
        string filePath = @"C:\emq\emqsongsmetadata\ReviewQueue (2).json";
        string contents = await File.ReadAllTextAsync(filePath);
        var rq = JsonSerializer.Deserialize<ReviewQueue[]>(contents)!;

        foreach (var r in rq)
        {
            var song = await DbManager.SelectSongs(
                new Song() { Links = new List<SongLink>() { new SongLink() { Url = r.url } } }, false);
            Console.WriteLine(song.Single().ToString());

            // int rows = await DbManager.SetSubmittedBy(r.url, r.submitted_by);
        }
    }

    [Test, Explicit]
    public async Task UploadMatchedSongs_MB()
    {
        string root = "C:\\emq\\matching\\mb";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            var songMatchInnerResults =
                JsonSerializer.Deserialize<List<SongMatchInnerResult>>(
                    await File.ReadAllTextAsync($"{dir}\\matched.json"),
                    Utils.JsoIndented)!;

            if (!File.Exists($"{dir}\\uploaded.json"))
            {
                await File.WriteAllTextAsync($"{dir}\\uploaded.json", "[]");
            }

            var uploaded =
                JsonSerializer.Deserialize<List<Uploadable>>(
                    await File.ReadAllTextAsync($"{dir}\\uploaded.json"),
                    Utils.JsoIndented)!;

            int oldCount = uploaded.Count;
            var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();

            for (int index = 0; index < songMatchInnerResults.Count; index++)
            {
                SongMatchInnerResult songMatchInnerResult = songMatchInnerResults[index];
                var uploadable = new Uploadable
                {
                    Path = songMatchInnerResult.SongMatch.Path,
                    MId = songMatchInnerResult.mIds.Single(),
                    MusicBrainzRecording = songMatchInnerResult.SongMatch.MusicBrainzRecording
                };

                if (uploaded.Any(x => x.Path == songMatchInnerResult.SongMatch.Path))
                {
                    continue;
                }

                if (midsWithSoundLinks.Any(x => x == uploadable.MId))
                {
                    if (uploaded.Count > oldCount)
                    {
                        Console.WriteLine("Skipping uploadable with existing mId: " +
                                          JsonSerializer.Serialize(uploadable, Utils.Jso));
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(uploadable.Path))
                {
                    Console.WriteLine("path is null: " + JsonSerializer.Serialize(uploadable, Utils.JsoIndented));
                    continue;
                }

                // if (uploaded.Count - oldCount >= 100)
                // {
                //     break;
                // }

                // Console.WriteLine(uploadable.Path);

                string extension = Path.GetExtension(uploadable.Path);
                if (!extension.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) &&
                    !extension.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                {
                    // Console.WriteLine("skipping non .mp3: " + uploadable.Path);
                    // continue;

                    // string sha1Str = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(uploadable.Path)));

                    // string newPath = uploadable.Path.Replace(Path.GetExtension(uploadable.Path), ".mp3");
                    string newPath = $"M:/a/mb/converted/{uploadable.MusicBrainzRecording}.mp3";
                    if (!File.Exists(newPath))
                    {
                        Console.WriteLine("converting non .mp3: " + uploadable.Path);
                        FFMpegArguments
                            .FromFileInput(uploadable.Path)
                            .OutputToFile(newPath, false, options => options
                                .WithAudioCodec(AudioCodec.LibMp3Lame)
                                .WithAudioBitrate(192)
                                .WithTagVersion())
                            .ProcessSynchronously();
                    }

                    if (uploaded.Any(x => x.Path == newPath))
                    {
                        continue;
                    }

                    uploadable.Path = newPath;
                }

                string recordingStoragePath = $"M:/a/mb/recordingstorage/{uploadable.MusicBrainzRecording}{extension}";
                if (!File.Exists(recordingStoragePath))
                {
                    File.Copy(uploadable.Path, recordingStoragePath);
                }

                string catboxUrl = await SelfUploader.Upload(uploadable, extension);
                uploadable.ResultUrl = catboxUrl;
                Console.WriteLine(catboxUrl);
                if (!catboxUrl.EndsWith(extension))
                {
                    Console.WriteLine("invalid resultUrl: " + JsonSerializer.Serialize(uploadable, Utils.JsoIndented));
                    continue;
                }

                var songLite = (await DbManager.SelectSongs(new Song { Id = uploadable.MId }, false)).Single()
                    .ToSongLite();
                uploadable.SongLite = songLite;
                uploaded.Add(uploadable);

                await File.WriteAllTextAsync($"{dir}\\uploaded.json",
                    JsonSerializer.Serialize(uploaded, Utils.JsoIndented));

                await Task.Delay(0000);
            }

            if (uploaded.Count - oldCount > 0)
            {
                await File.WriteAllTextAsync($"{dir}\\uploaded_backup_{DateTime.UtcNow:yyyyMMddTHHmmss}.json",
                    JsonSerializer.Serialize(uploaded, Utils.JsoIndented));
            }

            Console.WriteLine($"Uploaded {uploaded.Count - oldCount} files in {dir}.");
        }
    }

    [Test, Explicit]
    public async Task SubmitUploadedJsonForReview()
    {
        string root = "C:\\emq\\matching\\generic\\";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            // string submittedBy = Path.GetFileName(dir)!;

            var uploaded =
                JsonSerializer.Deserialize<List<Uploadable>>(
                    await File.ReadAllTextAsync($"{dir}\\uploaded.json"),
                    Utils.JsoIndented)!;

            var dup = uploaded.SelectMany(x => uploaded.Where(y => y.MId == x.MId && y.ResultUrl != x.ResultUrl))
                .ToList();
            await File.WriteAllTextAsync($"{dir}\\uploaded_dup.json",
                JsonSerializer.Serialize(dup, Utils.JsoIndented));

            var dup2 = uploaded.SelectMany(x => uploaded.Where(y => y.ResultUrl == x.ResultUrl && y.MId != x.MId))
                .ToList();
            await File.WriteAllTextAsync($"{dir}\\uploaded_dup2.json",
                JsonSerializer.Serialize(dup2, Utils.JsoIndented));

            await Parallel.ForEachAsync(uploaded,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
                async (uploadable, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(uploadable.ResultUrl) && uploadable.ResultUrl.EndsWith(".mp3"))
                    {
                        if (!dup.Any(x => x.MId == uploadable.MId))
                        {
                            var songLink = new SongLink()
                            {
                                Url = uploadable.ResultUrl,
                                Type = SongLinkType.Catbox,
                                IsVideo = false,
                                SubmittedBy = Constants.RobotName
                            };

                            int rqId = await DbManager.InsertReviewQueue(uploadable.MId, songLink);

                            var analyserResult = await MediaAnalyser.Analyse(uploadable.Path);
                            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                                analyserResult: analyserResult);
                        }
                        else
                        {
                            Console.WriteLine("Skipping duplicate uploadable: " +
                                              JsonSerializer.Serialize(uploadable, Utils.Jso));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid uploadable: " +
                                          JsonSerializer.Serialize(uploadable, Utils.Jso));
                    }
                });
        }
    }

    [Test, Explicit]
    public async Task SubmitUploadedJsonForReview_MB()
    {
        string root = "C:\\emq\\matching\\mb\\";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            // string submittedBy = Path.GetFileName(dir)!;

            var uploaded =
                JsonSerializer.Deserialize<List<Uploadable>>(
                    await File.ReadAllTextAsync($"{dir}\\uploaded.json"),
                    Utils.JsoIndented)!;

            var dup = uploaded.SelectMany(x => uploaded.Where(y => y.MId == x.MId && y.ResultUrl != x.ResultUrl))
                .ToList();
            await File.WriteAllTextAsync($"{dir}\\uploaded_dup.json",
                JsonSerializer.Serialize(dup, Utils.JsoIndented));

            var dup2 = uploaded.SelectMany(x => uploaded.Where(y => y.ResultUrl == x.ResultUrl && y.MId != x.MId))
                .ToList();
            await File.WriteAllTextAsync($"{dir}\\uploaded_dup2.json",
                JsonSerializer.Serialize(dup2, Utils.JsoIndented));

            var recordingMids = await DbManager.GetRecordingMids();
            var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();

            await Parallel.ForEachAsync(uploaded,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
                async (uploadable, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(uploadable.ResultUrl) && (uploadable.ResultUrl.EndsWith(".mp3") ||
                                                                             uploadable.ResultUrl.EndsWith(".ogg")))
                    {
                        if (!dup.Any(x => x.MId == uploadable.MId))
                        {
                            if (recordingMids.TryGetValue(uploadable.MusicBrainzRecording!, out int mId))
                            {
                                if (!midsWithSoundLinks.Contains(mId))
                                {
                                    var songLink = new SongLink()
                                    {
                                        Url = uploadable.ResultUrl,
                                        Type = SongLinkType.Self,
                                        IsVideo = false,
                                        SubmittedBy = Constants.RobotName
                                    };

                                    int rqId = await DbManager.InsertReviewQueue(mId, songLink);

                                    var analyserResult = await MediaAnalyser.Analyse(uploadable.Path);
                                    await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                                        analyserResult: analyserResult);
                                }
                                else
                                {
                                    // Console.WriteLine($"skipping existing mId {mId} {uploadable.Path}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Skipping recording not in db: " +
                                                  JsonSerializer.Serialize(uploadable, Utils.Jso));
                            }
                        }
                        else
                        {
                            Console.WriteLine("Skipping duplicate uploadable: " +
                                              JsonSerializer.Serialize(uploadable, Utils.Jso));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid uploadable: " +
                                          JsonSerializer.Serialize(uploadable, Utils.Jso));
                    }
                });
        }
    }

    [Test, Explicit]
    public async Task ImportTora()
    {
        await ToraImporter.ImportTora();
    }

    [Test, Explicit]
    public async Task ImportGeneric()
    {
        await GenericImporter.ImportGeneric();
    }

    [Test, Explicit]
    public async Task ImportGenericWithDir()
    {
        string root = "";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            await GenericImporter.ImportGenericWithDir(dir, 1);
        }
    }

    [Test, Explicit]
    public async Task ImportKnownArtist()
    {
        await KnownArtistImporter.ImportKnownArtist();
    }

    [Test, Explicit]
    public async Task ImportKnownArtistWithDir()
    {
        string root = "M:\\!matching\\artist";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            await KnownArtistImporter.ImportKnownArtistWithDir(dir, 3);
        }
    }

    [Test, Explicit]
    public async Task ImportACG()
    {
        await ACGImporter.ImportACG();
    }

    [Test, Explicit]
    public async Task Conv1_FixCUEEncoding()
    {
        await Conv.FixCUEEncoding();
    }

    [Test, Explicit]
    public async Task Conv2_SplitTracks()
    {
        await Conv.SplitTracks();
    }

    [Test, Explicit]
    public async Task ImportMusicBrainzRelease()
    {
        // string dir = @"L:\olil355 - Copy";
        // string dir = @"G:\Music\Kitto, Sumiwataru Asairo Yori mo\Kitto, Sumiwataru Asairo Yorimo, Music Collection";
        // string dir = @"G:\Music";
        // string dir = @"H:\mb\Music";
        string dir = @"M:\a\mb\gmusicreplica";
        // string dir = @"M:\a\c";
        var regex = new Regex("", RegexOptions.Compiled);
        List<string> extensions = new()
        {
            "mp3",
            "flac",
            "tak",
            "ape",
            "wav",
            "tta",
            "m4a",
            "ogg",
        };

        var songMatches = SongMatcher.ParseSongFile(dir, regex, extensions, false, true, false);
        await SongMatcher.MatchMusicBrainzRelease(songMatches, "C:\\emq\\matching\\mb\\gmusicreplica2", "", false);
    }

    [Test, Explicit]
    public async Task ListNoDisuku()
    {
        List<string> isInvalidFormat =
            JsonSerializer.Deserialize<List<string>>(
                await File.ReadAllTextAsync(
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\isInvalidFormat.json"),
                Utils.JsoIndented)!;

        List<string> splitFiles =
            JsonSerializer.Deserialize<List<string>>(
                await File.ReadAllTextAsync(
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\splitFiles.json"),
                Utils.JsoIndented)!;

        string path = @"L:\olil355 - Copy\";
        var dirs = Directory.EnumerateDirectories(path);
        foreach (string dir in dirs)
        {
            // Console.WriteLine(dir);
            var dirs2 = Directory.EnumerateDirectories(dir);
            // Console.WriteLine(dirs2.Count());
            foreach (string dir2 in dirs2)
            {
                string final = $"file:///{dir2.Replace(" ", "%20")}";
                if (isInvalidFormat.Any(x => x.Contains(dir2)))
                {
                    Console.WriteLine($"skipping invalidFormat: {final}");
                    continue;
                }

                if (splitFiles.Any(x => x.Contains(dir2)))
                {
                    Console.WriteLine($"skipping splitFiles: {final}");
                    continue;
                }

                var dir2BinFiles = Directory.EnumerateFiles(dir2, "*.bin",
                    new EnumerationOptions() { RecurseSubdirectories = true });
                if (dir2BinFiles.Any())
                {
                    Console.WriteLine($"skipping bin: {final}");
                    continue;
                }

                var dir2ImgFiles = Directory.EnumerateFiles(dir2, "*.img",
                    new EnumerationOptions() { RecurseSubdirectories = true });
                if (dir2ImgFiles.Any())
                {
                    Console.WriteLine($"skipping img: {final}");
                    continue;
                }

                // Console.WriteLine(dir2);
                var dirs3 = Directory.EnumerateDirectories(dir2, "Disuku*",
                    new EnumerationOptions { RecurseSubdirectories = true });

                if (!dirs3.Any())
                {
                    var dir2CueFiles = Directory.EnumerateFiles(dir2, "*.cue",
                        new EnumerationOptions() { RecurseSubdirectories = true });
                    if (!dir2CueFiles.Any())
                    {
                        // checked all of these 2023-11-11
                        // Console.WriteLine($"skipping splitFiles2: {final}");
                        continue;
                    }

                    Console.WriteLine(final);
                }
            }
        }
    }
}
