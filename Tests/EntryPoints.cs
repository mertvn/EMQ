using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Server.Db.Imports;
using EMQ.Server.Db.Imports.EGS;
using EMQ.Server.Db.Imports.GGVC;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Newtonsoft.Json;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

public class EntryPoints
{
    [Test, Explicit]
    public async Task a_Integration_Full()
    {
        ServerUtils.Client.BaseAddress = new Uri("https://localhost:7021/");

        HttpResponseMessage res = await ServerUtils.Client.PostAsJsonAsync("Auth/CreateSession",
            new ReqCreateSession(
                "p0",
                "",
                new PlayerVndbInfo() { VndbId = "", VndbApiToken = "" }));

        ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
        var session = resCreateSession!.Session;

        for (int i = 0; i < 100; i++)
        {
            ReqCreateRoom req = new(session.Token, $"r{i}", "", new QuizSettings(){NumSongs = 40});
            HttpResponseMessage res1 = await ServerUtils.Client.PostAsJsonAsync("Quiz/CreateRoom", req);
            int roomId = await res1.Content.ReadFromJsonAsync<int>();

            HttpResponseMessage res2 = await ServerUtils.Client.PostAsJsonAsync("Quiz/StartQuiz",
                new ReqStartQuiz(session.Token, roomId));
        }
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteMstJson()
    {
        await File.WriteAllTextAsync("mst.json", await DbManager.SelectAutocompleteMst());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteCJson()
    {
        await File.WriteAllTextAsync("c.json", await DbManager.SelectAutocompleteC());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteAJson()
    {
        await File.WriteAllTextAsync("a.json", await DbManager.SelectAutocompleteA());
    }

    [Test, Explicit]
    public async Task ImportVndbData()
    {
        await VndbImporter.ImportVndbData();
    }

    [Test, Explicit]
    public async Task ImportEgsData()
    {
        await EgsImporter.ImportEgsData();
    }

    [Test, Explicit]
    public async Task GenerateSong()
    {
        await File.WriteAllTextAsync("Song.json", await DbManager.ExportSong());
    }

    [Test, Explicit]
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    public async Task GenerateSongLite()
    {
#pragma warning disable CS0162
        if (true)
        {
            await File.WriteAllTextAsync("SongLite.json", await DbManager.ExportSongLite());
        }
        else

        {
            var adminPassword = Environment.GetEnvironmentVariable("EMQ_ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new Exception("EMQ_ADMIN_PASSWORD is null");
            }

            var serverUrl = "https://emq.up.railway.app";
            var songLite =
                await ServerUtils.Client.GetStringAsync(
                    $"{serverUrl}/Mod/ExportSongLite?adminPassword={adminPassword}");

            if (string.IsNullOrWhiteSpace(songLite))
            {
                throw new Exception("songLite is null");
            }

            await File.WriteAllTextAsync("SongLite.json", songLite);
        }
#pragma warning restore CS0162
    }

    [Test, Explicit]
    public async Task GenerateReviewQueue()
    {
        await File.WriteAllTextAsync("ReviewQueue.json", await DbManager.ExportReviewQueue());
    }

    [Test, Explicit]
    public async Task ImportSongLite()
    {
        var deserialized =
            JsonConvert.DeserializeObject<List<SongLite>>(
                await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\SongLite.json"));
        await DbManager.ImportSongLite(deserialized!);
    }

    // music ids can change between vndb imports, so this doesn't work correctly right now
    // todo add songlite to rq
    // [Test, Explicit]
    // public async Task ImportReviewQueue()
    // {
    //     var deserialized =
    //         JsonConvert.DeserializeObject<List<ReviewQueue>>(
    //             await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\ReviewQueue.json"));
    //     await DbManager.ImportReviewQueue(deserialized!);
    // }

    [Test, Explicit]
    public async Task ApproveReviewQueueItem()
    {
        var rqIds = Enumerable.Range(112, 60).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Approved);
        }
    }

    [Test, Explicit]
    public async Task RejectReviewQueueItem()
    {
        var rqIds = Enumerable.Range(61, 1).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Rejected);
        }
    }

    [Test, Explicit]
    public async Task BackupSongFilesUsingSongLite()
    {
        const string baseDownloadDir = "C:\\emq\\emqsongsbackup";
        Directory.CreateDirectory(baseDownloadDir);

        const string songLitePath = "C:\\emq\\emqsongsmetadata\\SongLite.json";
        var songLites =
            JsonSerializer.Deserialize<List<SongLite>>(await File.ReadAllTextAsync(songLitePath), Utils.JsoIndented)!;

        int dlCount = 0;
        foreach (var songLite in songLites)
        {
            foreach (var link in songLite.Links)
            {
                string filePath = $"{baseDownloadDir}\\{link.Url.LastSegment()}";

                if (!File.Exists(filePath))
                {
                    bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(link.Url));
                    if (success)
                    {
                        dlCount += 1;
                        await Task.Delay(20000);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        Console.WriteLine($"Downloaded {dlCount} files.");
    }

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
    public async Task UploadGGVCMatched()
    {
        var ggvcInnerResults =
            JsonSerializer.Deserialize<List<GGVCInnerResult>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc3\\matched.json"),
                Utils.JsoIndented)!;

        var uploaded =
            JsonSerializer.Deserialize<List<Uploadable>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc3\\uploaded.json"),
                Utils.JsoIndented)!;

        int oldCount = uploaded.Count;
        var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();

        for (int index = 0; index < ggvcInnerResults.Count; index++)
        {
            GGVCInnerResult ggvcInnerResult = ggvcInnerResults[index];
            var uploadable = new Uploadable
            {
                Path = ggvcInnerResult.GGVCSong.Path, MId = ggvcInnerResult.mIds.Single()
            };

            if (uploaded.Any(x => x.Path == ggvcInnerResult.GGVCSong.Path))
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

            if (uploaded.Count - oldCount >= 100)
            {
                break;
            }

            string catboxUrl = await CatboxUploader.Upload(uploadable);
            uploadable.ResultUrl = catboxUrl;
            Console.WriteLine(catboxUrl);
            if (!catboxUrl.EndsWith(".mp3"))
            {
                Console.WriteLine("invalid resultUrl: " + JsonSerializer.Serialize(uploadable, Utils.JsoIndented));
                continue;
            }

            var songLite = Song.ToSongLite((await DbManager.SelectSongs(new Song { Id = uploadable.MId })).Single());
            uploadable.SongLite = songLite;
            uploaded.Add(uploadable);

            await File.WriteAllTextAsync("C:\\emq\\ggvc3\\uploaded.json",
                JsonSerializer.Serialize(uploaded, Utils.JsoIndented));

            await Task.Delay(20000);
        }

        await File.WriteAllTextAsync($"C:\\emq\\ggvc3\\uploaded_backup_{DateTime.UtcNow:yyyyMMddTHHmmss}.json",
            JsonSerializer.Serialize(uploaded, Utils.JsoIndented));

        Console.WriteLine($"Uploaded {uploaded.Count - oldCount} files.");
    }

    [Test, Explicit]
    public async Task SubmitUploadedJsonForReview()
    {
        var uploaded =
            JsonSerializer.Deserialize<List<Uploadable>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc3\\uploaded.json"),
                Utils.JsoIndented)!;

        var dup = uploaded.SelectMany(x => uploaded.Where(y => y.MId == x.MId && y.ResultUrl != x.ResultUrl)).ToList();
        await File.WriteAllTextAsync("C:\\emq\\ggvc3\\uploaded_dup.json",
            JsonSerializer.Serialize(dup, Utils.JsoIndented));

        var dup2 = uploaded.SelectMany(x => uploaded.Where(y => y.ResultUrl == x.ResultUrl && y.MId != x.MId)).ToList();
        await File.WriteAllTextAsync("C:\\emq\\ggvc3\\uploaded_dup2.json",
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
                    await DbManager.InsertReviewQueue(uploadable.MId, songLink, "GGVC");
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

    // todo pgrestore pgdump tests
}
