using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
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
using FluentMigrator.Runner;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Npgsql;
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
                var roomId = await res1.Content.ReadFromJsonAsync<Guid>();

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
            Guid roomId = Guid.Empty;
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
                    roomId = await res1.Content.ReadFromJsonAsync<Guid>();
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
    public async Task GenerateSongLite()
    {
        bool useLocal = false;
        if (useLocal)
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

            string serverUrl = "https://emq.up.railway.app";
            string songLite =
                await ServerUtils.Client.GetStringAsync(
                    $"{serverUrl}/Mod/ExportSongLite?adminPassword={adminPassword}");

            if (string.IsNullOrWhiteSpace(songLite))
            {
                throw new Exception("songLite is null");
            }

            await File.WriteAllTextAsync("SongLite.json", songLite);
        }
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
        int[] rqIds = Enumerable.Range(3, 1600).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Approved);
        }
    }

    [Test, Explicit]
    public async Task RejectReviewQueueItem()
    {
        int[] rqIds = Enumerable.Range(1, 1).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Rejected, "");
        }
    }

    [Test, Explicit]
    public async Task AnalyzeReviewQueueItems()
    {
        var rqs = await DbManager.FindRQs(DateTime.MinValue, DateTime.MaxValue);
        foreach (RQ rq in rqs)
        {
            if (rq.analysis == "Pending")
            {
                string filePath = System.IO.Path.GetTempPath() + rq.url.LastSegment();

                bool dlSuccess = await ExtensionMethods.DownloadFile2(filePath, new Uri(rq.url));
                if (dlSuccess)
                {
                    var analyserResult = await MediaAnalyser.Analyse(filePath);
                    System.IO.File.Delete(filePath);

                    await DbManager.UpdateReviewQueueItem(rq.id, ReviewQueueStatus.Pending,
                        analyserResult: analyserResult);
                }
            }
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

    [Test, Explicit]
    public async Task FreshSetup()
    {
        // Requirements: DATABASE_URL env var set, with the database name as 'EMQ'; tar, zstd, postgres(psql) all installed/in PATH
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        Constants.ImportDateVndb = DateTime.UtcNow.ToString("yyyy-MM-dd");

        bool recreateEmqDb = true;
        string emqDbName = "EMQ";

        bool recreateVndbDb = true;
        string vndbDbName = "vndbforemq";

        string vndbDumpDirectory = $"C:/emq/vndbdumps/{DateTime.UtcNow:yyyy-MM-dd}";
        Directory.CreateDirectory(vndbDumpDirectory);

        string executingDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(vndbDumpDirectory);
        string dbDumpFilePath = $"{vndbDumpDirectory}/vndb-db-latest.tar.zst";

        if (!ConnectionHelper.GetConnectionString().Contains("DATABASE=EMQ;", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Database name in the connstr must be 'EMQ'");
        }

        Console.WriteLine(
            $"StartSection downloading VNDB dump: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        if (!File.Exists(dbDumpFilePath))
        {
            bool dlSuccess = await ExtensionMethods.DownloadFile2(dbDumpFilePath,
                new Uri("https://dl.vndb.org/dump/vndb-db-latest.tar.zst"));
            if (!dlSuccess)
            {
                throw new Exception("Failed to download VNDB db dump");
            }
        }

        Console.WriteLine(
            $"StartSection extracting zstd: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        string tarFilePath = dbDumpFilePath.Replace(".zst", "");
        if (!File.Exists(tarFilePath))
        {
            await Process.Start(new ProcessStartInfo()
            {
                FileName = "zstd", Arguments = $"-d {dbDumpFilePath}", CreateNoWindow = true, UseShellExecute = false,
            })!.WaitForExitAsync();
        }

        if (!File.Exists(tarFilePath))
        {
            throw new Exception("zstd failed");
        }

        Console.WriteLine(
            $"StartSection extracting tar: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        string vndbImportScriptPath = $@"{vndbDumpDirectory}/import.sql";
        if (!File.Exists(vndbImportScriptPath))
        {
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "tar",
                    Arguments = $"--force-local -xvf {tarFilePath} -C {vndbDumpDirectory}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                }
            };
            proc.Start();

            File.Delete("output_tar.txt");
            while (!proc.StandardOutput.EndOfStream)
            {
                await File.AppendAllTextAsync("output_tar.txt", await proc.StandardOutput.ReadLineAsync() + "\n");
            }
        }

        if (!File.Exists(vndbImportScriptPath))
        {
            throw new Exception("tar failed");
        }

        Console.WriteLine(
            $"StartSection recreateEmqDb: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        if (recreateEmqDb)
        {
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()
                             .Replace("DATABASE=EMQ;", "", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync($"DROP DATABASE IF EXISTS \"{emqDbName}\";");
                await connection.ExecuteAsync(
                    $"CREATE DATABASE \"{emqDbName}\" WITH OWNER = postgres ENCODING = 'UTF8' CONNECTION LIMIT = -1 IS_TEMPLATE = False;");

                var serviceProvider = new ServiceCollection()
                    .AddFluentMigratorCore()
                    .ConfigureRunner(rb => rb
                        .AddPostgres()
                        .WithGlobalConnectionString(ConnectionHelper.GetConnectionString())
                        .ScanIn(Assembly.GetAssembly(typeof(ServerState))).For.Migrations())
                    .AddLogging(lb => lb.AddFluentMigratorConsole())
                    .BuildServiceProvider(false);

                using (var scope = serviceProvider.CreateScope())
                {
                    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                    runner.MigrateUp();
                }
            }
        }

        Console.WriteLine(
            $"StartSection recreateVndbDb: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        if (recreateVndbDb)
        {
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()
                             .Replace("DATABASE=EMQ;", "", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync($"DROP DATABASE IF EXISTS \"{vndbDbName}\";");
                await connection.ExecuteAsync(
                    $"CREATE DATABASE \"{vndbDbName}\" WITH OWNER = postgres ENCODING = 'UTF8' CONNECTION LIMIT = -1 IS_TEMPLATE = False;");
            }

            Environment.SetEnvironmentVariable("PGPASSWORD", "postgres");
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "psql",
                    Arguments = $"-U postgres -d {vndbDbName} -f {vndbImportScriptPath}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                }
            };
            proc.Start();

            File.Delete("output_psql.txt");
            while (!proc.StandardOutput.EndOfStream)
            {
                await File.AppendAllTextAsync("output_psql.txt", await proc.StandardOutput.ReadLineAsync() + "\n");
            }

            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()
                             .Replace("DATABASE=EMQ;", $"DATABASE={vndbDbName};", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync(
                    $"CREATE MATERIALIZED VIEW tags_vn_inherit (tag, vid, rating, spoiler) AS\n    -- Group votes to generate a list of directly-upvoted (vid, tag) pairs.\n    -- This is essentually the same as the tag listing on VN pages.\n    WITH RECURSIVE t_avg(tag, vid, vote, spoiler) AS (\n        SELECT tv.tag, tv.vid, AVG(tv.vote)::real, CASE WHEN COUNT(tv.spoiler) = 0 THEN MIN(t.defaultspoil) ELSE AVG(tv.spoiler)::real END\n          FROM tags_vn tv\n          JOIN tags t ON t.id = tv.tag\n          LEFT JOIN users u ON u.id = tv.uid\n         WHERE NOT tv.ignore AND (u.id IS NULL OR u.perm_tag)\n         GROUP BY tv.tag, tv.vid\n        HAVING AVG(tv.vote) > 0\n    -- Add parent tags\n    ), t_all(lvl, tag, vid, vote, spoiler) AS (\n        SELECT 15, * FROM t_avg\n        UNION ALL\n        SELECT ta.lvl-1, tp.parent, ta.vid, ta.vote, ta.spoiler\n          FROM t_all ta\n          JOIN tags_parents tp ON tp.id = ta.tag\n         WHERE ta.lvl > 0\n    )\n    -- Merge\n    SELECT tag, vid, AVG(vote)\n         , (CASE WHEN MIN(spoiler) > 1.3 THEN 2 WHEN MIN(spoiler) > 0.4 THEN 1 ELSE 0 END)::smallint\n      FROM t_all\n     WHERE tag IN(SELECT id FROM tags WHERE searchable)\n     GROUP BY tag, vid;");
                await connection.ExecuteAsync(
                    "CREATE INDEX tags_vn_inherit_tag_vid ON tags_vn_inherit (tag, vid);");
            }
        }

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()
                         .Replace("DATABASE=EMQ;", $"DATABASE={vndbDbName};", StringComparison.OrdinalIgnoreCase)))
        {
            var importTest = await connection.QueryAsync("SELECT * FROM vn LIMIT 1");
            // Console.WriteLine(JsonConvert.SerializeObject(importTest));
            if (!importTest.Any())
            {
                throw new Exception("Something went wrong when importing VNDB db dump");
            }

            string vndbDir = $@"C:/emq/vndb/{DateTime.UtcNow:yyyy-MM-dd}";
            Directory.CreateDirectory(vndbDir);

            // List<string> ignoredQueries = new()
            // {
            //     // "EMQ vnTagInfo"
            // };

            Directory.SetCurrentDirectory(executingDirectory);
            string vndbQueriesDir = @"../../../../Queries/VNDB";
            foreach (string filePath in Directory.GetFiles(vndbQueriesDir))
            {
                string filename = Path.GetFileNameWithoutExtension(filePath);
                // if (ignoredQueries.Contains(filename))
                // {
                //     Console.WriteLine($"Skipping query: {filename}");
                //     continue;
                // }

                string sql = await File.ReadAllTextAsync(filePath);
                Console.WriteLine(
                    $"StartSection running query: {filename}: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

                var queryResult = await connection.QueryAsync(sql, commandTimeout: 1000);
                foreach (dynamic o in queryResult)
                {
                    if (!string.IsNullOrWhiteSpace(o.TVIs))
                    {
                        // can't serialize this one correctly as dynamic because it is serialized json already
                        string str = (string)o.TVIs.ToString();
                        var tvis = JsonConvert.DeserializeObject<List<TVI>>(str)!;
                        o.TVIs = tvis;
                    }
                }

                await File.WriteAllTextAsync($"{vndbDir}/{filename}.json", JsonConvert.SerializeObject(queryResult));
            }

            foreach (string filePath in Directory.GetFiles(vndbQueriesDir))
            {
                string filename = Path.GetFileNameWithoutExtension(filePath);
                if (!File.Exists($"{vndbDir}/{filename}.json"))
                {
                    throw new Exception($"Missing query result: {vndbDir}/{filename}.json");
                }
            }

            Console.WriteLine(
                $"StartSection Test_Batch: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
            var vndbStaffNotesParserTests = new VNDBStaffNotesParserTests();
            vndbStaffNotesParserTests.Setup();
            await vndbStaffNotesParserTests.Test_Batch();

            Console.WriteLine(
                $"StartSection ImportVndbData: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
            var entryPoints = new EntryPoints();
            await entryPoints.ImportVndbData();
            Console.WriteLine(
                $"StartSection ImportSongLite: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
            await entryPoints.ImportSongLite();
            Console.WriteLine(
                $"StartSection ImportEgsData: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
            await entryPoints.ImportEgsData();
        }

        Console.WriteLine(
            $"StartSection cleanup: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        Directory.Delete($"{vndbDumpDirectory}/db", true);
        foreach (string filePath in Directory.GetFiles(vndbDumpDirectory))
        {
            if (!filePath.EndsWith(".zst"))
            {
                File.Delete(filePath);
            }
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"StartSection finished: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
    }

    [Test, Explicit]
    public async Task PgDump()
    {
        Directory.SetCurrentDirectory(@"C:/emq/dbbackups");
        var builder = ConnectionHelper.GetConnectionStringBuilder();
        Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

        string dumpFileName = $"pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.tar";
        var proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "pg_dump",
                Arguments =
                    $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"t\" -f {dumpFileName} -d \"{builder.Database}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            }
        };
        proc.Start();

        File.Delete("output_pg_dump.txt");
        while (!proc.StandardOutput.EndOfStream)
        {
            await File.AppendAllTextAsync("output_pg_dump.txt", await proc.StandardOutput.ReadLineAsync() + "\n");
        }

        if (!File.Exists(dumpFileName))
        {
            throw new Exception("pg_dump failed");
        }
    }

    [Test, Explicit]
    public async Task PgRestore()
    {
        Directory.SetCurrentDirectory(@"C:/emq/dbbackups");
        var builder = ConnectionHelper.GetConnectionStringBuilder();
        Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

        string dumpFileName = "pgdump_2023-06-21_EMQ@localhost.tar";
        var proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "pg_restore",
                Arguments =
                    $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"t\" -d \"{builder.Database}\" \"{dumpFileName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            }
        };
        proc.Start();

        File.Delete("output_pg_restore.txt");
        while (!proc.StandardOutput.EndOfStream)
        {
            await File.AppendAllTextAsync("output_pg_restore.txt", await proc.StandardOutput.ReadLineAsync() + "\n");
        }
    }
}
