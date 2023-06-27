using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
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

                var songLite = (await DbManager.SelectSongs(new Song { Id = uploadable.MId })).Single().ToSongLite();
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
    public async Task SubmitUploadedJsonForReview()
    {
        string root = "C:\\emq\\matching\\generic\\";
        string[] dirs = Directory.GetDirectories(root);
        foreach (string dir in dirs)
        {
            string submittedBy = Path.GetFileName(dir)!;

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

            foreach (Uploadable uploadable in uploaded)
            {
                if (!string.IsNullOrWhiteSpace(uploadable.ResultUrl) && uploadable.ResultUrl.EndsWith(".mp3"))
                {
                    if (!dup.Any(x => x.MId == uploadable.MId))
                    {
                        var songLink = new SongLink()
                        {
                            Url = uploadable.ResultUrl, Type = SongLinkType.Catbox, IsVideo = false
                        };

                        int rqId = await DbManager.InsertReviewQueue(uploadable.MId, songLink, submittedBy);

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
            }
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
}
