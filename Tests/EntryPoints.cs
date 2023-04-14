using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Imports.EGS;
using EMQ.Server.Db.Imports.SongMatching;
using EMQ.Server.Db.Imports.SongMatching.Common;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

public class EntryPoints
{
    [Test, Explicit]
    public async Task a_Integration_100RoomsWith1PlayerX100()
    {
        ServerUtils.Client.BaseAddress = new Uri("https://localhost:7021/");

        for (int outerIndex = 0; outerIndex < 100; outerIndex++)
        {
            HttpResponseMessage res = await ServerUtils.Client.PostAsJsonAsync("Auth/CreateSession",
                new ReqCreateSession(
                    "p0",
                    "",
                    new PlayerVndbInfo() { VndbId = "", VndbApiToken = "" }));

            ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
            var session = resCreateSession!.Session;
            Assert.That(!string.IsNullOrWhiteSpace(session.Token));

            var hubConnection = new HubConnectionBuilder()
                .WithUrl(new Nav(ServerUtils.Client.BaseAddress.ToString()).ToAbsoluteUri("/QuizHub"),
                    options => { options.AccessTokenProvider = () => Task.FromResult(session.Token)!; })
                .WithAutomaticReconnect()
                .Build();

            await hubConnection.StartAsync();
            Assert.That(hubConnection.State == HubConnectionState.Connected);

            for (int i = 0; i < 100; i++)
            {
                ReqCreateRoom req = new(session.Token, $"r{i}", "", new QuizSettings() { NumSongs = 40 });
                HttpResponseMessage res1 = await ServerUtils.Client.PostAsJsonAsync("Quiz/CreateRoom", req);
                int roomId = await res1.Content.ReadFromJsonAsync<int>();

                HttpResponseMessage res2 = await ServerUtils.Client.PostAsJsonAsync("Quiz/JoinRoom",
                    new ReqJoinRoom(roomId, "", session.Player.Id));

                HttpResponseMessage res3 = await ServerUtils.Client.PostAsJsonAsync("Quiz/StartQuiz",
                    new ReqStartQuiz(session.Token, roomId));
            }
        }
    }

    [Test, Explicit]
    public async Task a_Integration_1RoomWith10PlayersX1000()
    {
        ServerUtils.Client.BaseAddress = new Uri("https://localhost:7021/");

        for (int outerIndex = 0; outerIndex < 1000; outerIndex++)
        {
            int numPlayers = 10;
            int roomId = -1;
            Session? p0Session = null;
            for (int currentPlayer = 0; currentPlayer < numPlayers; currentPlayer++)
            {
                HttpResponseMessage res = await ServerUtils.Client.PostAsJsonAsync("Auth/CreateSession",
                    new ReqCreateSession(
                        $"p{currentPlayer}",
                        "",
                        new PlayerVndbInfo() { VndbId = "", VndbApiToken = "" }));

                ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
                var session = resCreateSession!.Session;
                Assert.That(!string.IsNullOrWhiteSpace(session.Token));

                var hubConnection = new HubConnectionBuilder()
                    .WithUrl(new Nav(ServerUtils.Client.BaseAddress.ToString()).ToAbsoluteUri("/QuizHub"),
                        options => { options.AccessTokenProvider = () => Task.FromResult(session.Token)!; })
                    .WithAutomaticReconnect()
                    .Build();

                await hubConnection.StartAsync();
                Assert.That(hubConnection.State == HubConnectionState.Connected);

                if (currentPlayer == 0)
                {
                    ReqCreateRoom req = new(session.Token, $"r0", "", new QuizSettings() { NumSongs = 40 });
                    HttpResponseMessage res1 = await ServerUtils.Client.PostAsJsonAsync("Quiz/CreateRoom", req);
                    roomId = await res1.Content.ReadFromJsonAsync<int>();
                    p0Session = session;
                }

                HttpResponseMessage res2 = await ServerUtils.Client.PostAsJsonAsync("Quiz/JoinRoom",
                    new ReqJoinRoom(roomId, "", session.Player.Id));
            }

            HttpResponseMessage res3 = await ServerUtils.Client.PostAsJsonAsync("Quiz/StartQuiz",
                new ReqStartQuiz(p0Session!.Token, roomId));
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
            string? adminPassword = Environment.GetEnvironmentVariable("EMQ_ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new Exception("EMQ_ADMIN_PASSWORD is null");
            }

            string? serverUrl = "https://emq.up.railway.app";
            string? songLite =
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
        var rqIds = Enumerable.Range(868, 1600).ToArray();

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
        const string baseDownloadDir = "K:\\emq\\emqsongsbackup";
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
                        await Task.Delay(10000);
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

                var songLite =
                    Song.ToSongLite((await DbManager.SelectSongs(new Song { Id = uploadable.MId })).Single());
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

    // todo pgrestore pgdump tests
}
