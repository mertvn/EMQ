using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Client;
using EMQ.Client.Components;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Controllers;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.EGS;
using EMQ.Server.Db.Imports.MusicBrainz;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.MusicBrainz.Business;
using EMQ.Shared.Quiz.Entities.Concrete;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Npgsql;
using NUnit.Framework;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Session = EMQ.Shared.Auth.Entities.Concrete.Session;

namespace Tests;

public class EntryPoints
{
    [Test, Explicit]
    public async Task GenerateAutocompleteMstJson()
    {
        await File.WriteAllTextAsync("mst.json",
            await DbManager.SelectAutocompleteMst(new[] { SongSourceType.VN, SongSourceType.Other }));
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
    public async Task GenerateAutocompleteMtJson()
    {
        await File.WriteAllTextAsync("mt.json", await DbManager.SelectAutocompleteMt(SongSourceSongTypeMode.Vocals));
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteDeveloperJson()
    {
        await File.WriteAllTextAsync("developer.json", await DbManager.SelectAutocompleteDeveloper());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteCharacterJson()
    {
        await File.WriteAllTextAsync("character.json", await DbManager.SelectAutocompleteCharacter());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteIllustratorJson()
    {
        await File.WriteAllTextAsync("illustrator.json", await DbManager.SelectAutocompleteIllustrator());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteSeiyuuJson()
    {
        await File.WriteAllTextAsync("seiyuu.json", await DbManager.SelectAutocompleteSeiyuu());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteCollectionJson()
    {
        await File.WriteAllTextAsync("collection.json", await DbManager.SelectAutocompleteCollection());
    }

    [Test, Explicit]
    public async Task ImportVndbData()
    {
        const bool isIncremental = true;
        await VndbImporter.ImportVndbData(DateTime.Parse(Constants.ImportDateVndb), isIncremental);
    }

    // [Test, Explicit]
    // public async Task ImportVndbData_InsertPendingSongsWithSongLiteMusicIds()
    // {
    //     const bool isIncremental = true;
    //     await VndbImporter.ImportVndbData(DateTime.Parse(Constants.ImportDateVndb), isIncremental);
    //
    //     // todo path
    //     var deserialized =
    //         JsonConvert.DeserializeObject<List<SongLite>>(
    //             await File.ReadAllTextAsync("SongLite.json"));
    //
    //     Dictionary<string, SongLite> songLiteHashes = deserialized!.ToDictionary(y => y.EMQSongHash, y => y);
    //
    //     foreach (Song song in VndbImporter.PendingSongs)
    //     {
    //         if (songLiteHashes.TryGetValue(song.ToSongLite().EMQSongHash, out var songLite))
    //         {
    //             song.Id = songLite.MusicId;
    //             song.Links = songLite.Links;
    //             await DbManager.InsertSong(song);
    //
    //             if (songLite.SongStats != null)
    //             {
    //                 await DbManager.SetSongStats(song.Id, songLite.SongStats, null);
    //                 song.Stats = songLite.SongStats;
    //             }
    //         }
    //         else
    //         {
    //             Console.WriteLine($"incoming not found in SongLite: {song}");
    //         }
    //     }
    // }

    [Test, Explicit]
    public async Task SetMusicIdSeq()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            int lastId = await connection.QuerySingleAsync<int>("select id from music order by id desc limit 1");
            await connection.ExecuteAsync($"select pg_catalog.setval('music_id_seq', {lastId}, true);");
        }
    }

    [Test, Explicit]
    public async Task ImportEgsData()
    {
        await EgsImporter.ImportEgsData(DateTime.Parse(Constants.ImportDateEgs), false);
    }

    [Test, Explicit]
    public async Task ImportMusicBrainzData()
    {
        await MusicBrainzImporter.ImportMusicBrainzData(true, false);
    }

    // [Test, Explicit]
    // public async Task ImportMusicBrainzData_InsertPendingSongsWithSongLiteMusicIds()
    // {
    //     const bool isIncremental = true;
    //     await MusicBrainzImporter.ImportMusicBrainzData(isIncremental, false);
    //
    //     // todo path
    //     var deserialized =
    //         JsonConvert.DeserializeObject<List<SongLite_MB>>(
    //             await File.ReadAllTextAsync("SongLite_MB.json"));
    //
    //     Dictionary<Guid, SongLite_MB> songLiteHashes = deserialized!.ToDictionary(y => y.Recording, y => y);
    //
    //     foreach (Song song in MusicBrainzImporter.PendingSongs)
    //     {
    //         if (songLiteHashes.TryGetValue(song.ToSongLite_MB().Recording, out var songLite))
    //         {
    //             song.Id = songLite.MusicId;
    //             song.Links = songLite.Links;
    //             await DbManager.InsertSong(song);
    //
    //             if (songLite.SongStats != null)
    //             {
    //                 await DbManager.SetSongStats(song.Id, songLite.SongStats, null);
    //                 song.Stats = songLite.SongStats;
    //             }
    //         }
    //         else
    //         {
    //             Console.WriteLine($"incoming not found in SongLite: {song}");
    //         }
    //     }
    // }

    [Test, Explicit]
    public async Task GenerateSong()
    {
        await File.WriteAllTextAsync("Song.json", await DbManager.ExportSong());
    }

    [Test, Explicit]
    public async Task GenerateSongLite()
    {
        await File.WriteAllTextAsync("SongLite.json", await DbManager.ExportSongLite());
        // await TestSongLiteHealth();
    }

    [Test, Explicit]
    public async Task GenerateSongLite_MB()
    {
        await File.WriteAllTextAsync("SongLite_MB.json", await DbManager.ExportSongLite_MB());
        // await TestSongLite_MBHealth();
    }

    [Test, Explicit]
    public async Task GenerateReviewQueue()
    {
        await File.WriteAllTextAsync("ReviewQueue.json", await DbManager.ExportReviewQueue());
    }

    [Test, Explicit]
    public async Task GenerateReport()
    {
        await File.WriteAllTextAsync("Report.json", await DbManager.ExportReport());
    }

    [Test, Explicit]
    public async Task SetMelSubmittedByUsingReviewQueue()
    {
        const string reviewQueuePath = "C:\\emq\\emqsongsmetadata\\ReviewQueue.json";
        var reviewQueues =
            JsonSerializer.Deserialize<List<ReviewQueue>>(await File.ReadAllTextAsync(reviewQueuePath),
                Utils.JsoIndented)!;

        foreach (ReviewQueue reviewQueue in reviewQueues.Where(x =>
                     x.status == ReviewQueueStatus.Approved))
        {
            string submittedBy = reviewQueue.submitted_by;
            string url = reviewQueue.url;
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                const string sql = @"UPDATE music_external_link SET submitted_by = @submittedBy WHERE url = @url";
                await connection.ExecuteAsync(sql, new { submittedBy, url });
            }
        }
    }

    // [Test, Explicit]
    // public async Task ImportSongLite()
    // {
    //     throw new Exception("broken");
    //     // var deserialized =
    //     //     JsonConvert.DeserializeObject<List<SongLite>>(
    //     //         await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\SongLite.json"));
    //     // await DbManager.ImportSongLite(deserialized!);
    // }

    // [Test, Explicit]
    // public async Task TestSongLiteHealth()
    // {
    //     // todo path
    //     var deserialized = JsonConvert.DeserializeObject<List<SongLite>>(await File.ReadAllTextAsync("SongLite.json"))!;
    //
    //     var hashSet = new HashSet<string>();
    //     foreach (SongLite songLite in deserialized)
    //     {
    //         Assert.That(songLite.Titles.Any());
    //         // Assert.That(songLite.Links.Any());
    //         Assert.That(songLite.SourceVndbIds.Any());
    //         Assert.That(songLite.Artists.Any());
    //         Assert.That(songLite.EMQSongHash.Any());
    //         Assert.That(songLite.MusicId > 0);
    //
    //         Console.WriteLine(songLite.EMQSongHash);
    //         if (!hashSet.Add(songLite.EMQSongHash))
    //         {
    //             throw new Exception();
    //         }
    //     }
    // }

    // [Test, Explicit]
    // public async Task TestSongLite_MBHealth()
    // {
    //     // todo path
    //     var deserialized =
    //         JsonConvert.DeserializeObject<List<SongLite_MB>>(await File.ReadAllTextAsync("SongLite_MB.json"))!;
    //
    //     foreach (SongLite_MB songLite in deserialized)
    //     {
    //         Assert.That(songLite.Recording != default);
    //         // Assert.That(songLite.Links.Any());
    //         Assert.That(songLite.MusicId > 0);
    //     }
    // }

    // [Test, Explicit]
    // public async Task ImportSongLite_MB()
    // {
    //     var deserialized =
    //         JsonConvert.DeserializeObject<List<SongLite_MB>>(
    //             await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\SongLite_MB.json"));
    //     await DbManager.ImportSongLite_MB(deserialized!);
    // }

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
        const int s = 1;
        const int e = 1;
        for (int i = s; i <= e; i++)
        {
            await DbManager.UpdateReviewQueueItem(i, ReviewQueueStatus.Approved, "");
        }
    }

    [Test, Explicit]
    public async Task RejectReviewQueueItem()
    {
        const int s = 175;
        const int e = 175;
        for (int i = s; i <= e; i++)
        {
            await DbManager.UpdateReviewQueueItem(i, ReviewQueueStatus.Rejected, "");
        }
    }

    [Test, Explicit]
    public async Task UpdateEditQueueItem()
    {
        const int s = 24200;
        const int e = 26357;
        const ReviewQueueStatus status = ReviewQueueStatus.Approved;

        const int commitEveryN = 100;
        int lastCommit = 0;
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync(); // don't care about a memory leak here
        for (int i = s; i <= e; i++)
        {
            if (lastCommit >= commitEveryN)
            {
                lastCommit = 0;
                await transaction.CommitAsync();
                transaction = await connection.BeginTransactionAsync();
            }

            lastCommit += 1;
            bool success = await DbManager.UpdateEditQueueItem(transaction, i, status, "");
            if (!success)
            {
                throw new Exception();
            }
        }

        await transaction.CommitAsync();
    }

    [Test, Explicit]
    public async Task AnalyzeReviewQueueItems()
    {
        bool deleteAfter = false;

        var rqs = await DbManager.FindRQs(DateTime.MinValue, DateTime.MaxValue,
            SongSourceSongTypeMode.All.ToSongSourceSongTypes(),
            Enum.GetValues<ReviewQueueStatus>());
        foreach (RQ rq in rqs)
        {
            if (rq.analysis == "Pending"
                // || rq.analysis == "UnknownError"
               )
            {
                string filePath = System.IO.Path.GetTempPath() + rq.url.LastSegment();

                if (!File.Exists(filePath))
                {
                    bool dlSuccess = await ServerUtils.Client.DownloadFile(filePath, new Uri(rq.url));
                    if (!dlSuccess)
                    {
                        if (File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }

                        throw new Exception();
                    }
                }

                var analyserResult = await MediaAnalyser.Analyse(filePath);

                if (deleteAfter)
                {
                    System.IO.File.Delete(filePath);
                }

                await DbManager.UpdateReviewQueueItem(rq.id, ReviewQueueStatus.Pending,
                    analyserResult: analyserResult);
            }
        }
    }

    // todo download roomlogs

    [Test, Explicit]
    public async Task DownloadPendingReviewQueueItems()
    {
        var rqs = await DbManager.FindRQs(DateTime.MinValue, DateTime.MaxValue,
            SongSourceSongTypeMode.All.ToSongSourceSongTypes(),
            Enum.GetValues<ReviewQueueStatus>());
        foreach (RQ rq in rqs.Where(x => x.status == ReviewQueueStatus.Pending))
        {
            string filePath = $"{System.IO.Path.GetTempPath()}{rq.id}-{rq.url.LastSegment()}";
            if (!File.Exists(filePath))
            {
                bool dlSuccess = await ServerUtils.Client.DownloadFile(filePath, new Uri(rq.url));
                if (!dlSuccess)
                {
                    throw new Exception();
                }
            }
        }
    }

    // todo change all other code that expects emqsongsbackup or partial revert
    // deprecated in favor of EMQBackupScript
    // [Test, Explicit]
    // public async Task BackupSongFilesUsingBothSongLites()
    // {
    //     const string baseDownloadDir = "K:\\emq\\emqsongsbackup";
    //     Directory.CreateDirectory(baseDownloadDir);
    //
    //     const string songLitePath = "C:\\emq\\emqsongsmetadata\\SongLite.json";
    //     var songLites =
    //         JsonSerializer.Deserialize<List<SongLite>>(await File.ReadAllTextAsync(songLitePath),
    //             Utils.JsoIndented)!;
    //
    //     const string songLiteMbPath = "C:\\emq\\emqsongsmetadata\\SongLite_MB.json";
    //     var songLiteMbs =
    //         JsonSerializer.Deserialize<List<SongLite_MB>>(await File.ReadAllTextAsync(songLiteMbPath),
    //             Utils.JsoIndented)!;
    //
    //     int dlCount = 0;
    //     var allLinks = songLites.SelectMany(x => x.Links).Concat(songLiteMbs.SelectMany(y => y.Links));
    //     foreach (var link in allLinks)
    //     {
    //         string filePath;
    //         switch (link.Type)
    //         {
    //             case SongLinkType.Catbox:
    //                 filePath = $"{baseDownloadDir}\\catbox\\{link.Url.LastSegment()}";
    //                 break;
    //             case SongLinkType.Self:
    //                 link.Url = link.Url.ReplaceSelfhostLink();
    //                 if (link.Url.Contains("catbox"))
    //                 {
    //                     // skip mirror links
    //                     continue;
    //                 }
    //
    //                 filePath = $"{baseDownloadDir}\\selfhoststorage\\{link.Url.LastSegment()}";
    //                 break;
    //             case SongLinkType.Unknown:
    //             default:
    //                 throw new ArgumentOutOfRangeException();
    //         }
    //
    //         if (!File.Exists(filePath))
    //         {
    //             bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(link.Url));
    //             if (success)
    //             {
    //                 dlCount += 1;
    //                 if (link.Type is SongLinkType.Catbox)
    //                 {
    //                     await Task.Delay(10000);
    //                 }
    //                 else
    //                 {
    //                     await Task.Delay(1000);
    //                 }
    //             }
    //             else
    //             {
    //                 return;
    //             }
    //         }
    //     }
    //
    //     Console.WriteLine($"Downloaded {dlCount} files.");
    // }

    [Test, Explicit]
    public async Task InsertCatboxToSelfMirrorLinks()
    {
        const string songLitePath = "C:\\emq\\emqsongsmetadata\\SongLite.json";
        var songLites =
            JsonSerializer.Deserialize<List<SongLite>>(await File.ReadAllTextAsync(songLitePath),
                Utils.JsoIndented)!;
        foreach (SongLite songLite in songLites)
        {
            foreach (SongLink catboxLink in songLite.Links.Where(x => x.Type is SongLinkType.Catbox))
            {
                string mirrorUrl = catboxLink.Url.Replace("https://files.catbox.moe/",
                    $"{Constants.SelfhostAddress}/selfhoststorage/catbox/");
                // Console.WriteLine(mirrorUrl);

                await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
                {
                    var mids = (await connection.QueryAsync<int>(
                        "select music_id from music_external_link where url = @url",
                        new { url = catboxLink.Url })).ToList();
                    foreach (int mid in mids)
                    {
                        var selfLink = new SongLink
                        {
                            Url = mirrorUrl,
                            Type = SongLinkType.Self,
                            IsVideo = catboxLink.IsVideo,
                            Duration = catboxLink.Duration,
                            SubmittedBy = catboxLink.SubmittedBy,
                            Sha256 = catboxLink.Sha256
                        };
                        await DbManager.InsertSongLink(mid, selfLink, null);
                    }
                }
            }
        }
    }

    [Test, Explicit]
    public async Task UpdateMusicExternalLinkSha256Hashes()
    {
        const string baseDownloadDir = "K:\\emq\\emqsongsbackup";
        string[] filePaths =
            Directory.GetFiles(baseDownloadDir, "*.*", new EnumerationOptions() { RecurseSubdirectories = true });

        var dbSongs = await DbManager.GetRandomSongs(int.MaxValue, true);
        foreach (Song dbSong in dbSongs)
        {
            foreach (SongLink dbSongLink in dbSong.Links.Where(x => x.IsFileLink))
            {
                if (string.IsNullOrWhiteSpace(dbSongLink.Sha256))
                {
                    string? filePath = filePaths.FirstOrDefault(x => x.LastSegment() == dbSongLink.Url.LastSegment());
                    if (filePath != null)
                    {
                        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                        string? sha256 = CryptoUtils.Sha256Hash(fs);
                        Assert.That(sha256 != null);
                        Assert.That(sha256!.Any());

                        Console.WriteLine($"Setting {dbSongLink.Url} sha256 to {sha256}");
                        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
                        {
                            const string sql = @"UPDATE music_external_link SET sha256 = @sha256 WHERE url = @url";
                            await connection.ExecuteAsync(sql, new { sha256, url = dbSongLink.Url });
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Song backup not found: {dbSongLink.Url}");
                    }
                }
                else if (dbSongLink.Type == SongLinkType.Catbox &&
                         !dbSong.Links.Any(x => x.Type == SongLinkType.Self && x.Sha256 == dbSongLink.Sha256))
                {
                    Console.WriteLine($"Mirror is not found or corrupt: {dbSongLink.Url}");
                }
            }
        }
    }

    [Test, Explicit]
    public async Task FreshSetup()
    {
        // Requirements: DATABASE_URL env var set, with the database name as 'EMQ'; tar, zstd, postgres(psql) all installed/in PATH
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        if (Constants.ImportDateVndb != DateTime.UtcNow.ToString("yyyy-MM-dd"))
        {
            throw new Exception("VNDB import date is not set to today");
        }

        bool recreateEmqDb = false;
        recreateEmqDb = false;
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
            bool dlSuccess = await ServerUtils.Client.DownloadFile(dbDumpFilePath,
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
                    .Configure<RunnerOptions>(opt => { opt.Tags = new[] { "SONG" }; })
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

                var queryResult = await connection.QueryAsync(sql, commandTimeout: 10000);
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
            await new Setup().RunBeforeTests();
            var vndbStaffNotesParserTests = new VNDBStaffNotesParserTests();
            await vndbStaffNotesParserTests.Test_Batch();

            bool b = false;
            if (b)
            {
                Console.WriteLine(
                    $"StartSection ImportVndbData: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
                await VndbImporter.ImportVndbData(DateTime.Parse(Constants.ImportDateVndb), false);

                // var entryPoints = new EntryPoints();
                // Console.WriteLine(
                //     $"StartSection ImportEgsData: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
                // await entryPoints.ImportEgsData();
            }
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
        string envVar = "DATABASE_URL";
        // envVar = "EMQ_AUTH_DATABASE_URL";
        // envVar = "EMQ_VNDB_DATABASE_URL";

        var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
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
    public async Task PgDump_OneTable()
    {
        Directory.SetCurrentDirectory(@"C:/emq/dbbackups");
        string envVar = "DATABASE_URL";
        // envVar = "EMQ_AUTH_DATABASE_URL";
        // envVar = "EMQ_VNDB_DATABASE_URL";

        string table = "edit_queue";
        var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
        Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

        string dumpFileName = $"pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}-{table}.tar";
        var proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "pg_dump",
                Arguments =
                    $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"t\" -f {dumpFileName} -d \"{builder.Database}\" -t {table}",
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
        string envVar = "DATABASE_URL";
        // envVar = "EMQ_AUTH_DATABASE_URL";

        var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
        Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

        string dumpFileName = "pgdump_2025-04-02_EMQ@erogemusicquiz.com.tar";
        // dumpFileName = "pgdump_2024-02-19_vndbforemq@localhost.tar";
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

    [Test, Explicit]
    public async Task RecreateSchema()
    {
        if (ConnectionHelper.GetConnectionString().Contains("erogemusicquiz.com"))
        {
            throw new Exception("wrong db");
        }

        if (ConnectionHelper.GetConnectionString().Contains("AUTH"))
        {
            throw new Exception("wrong db");
        }

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql = @"
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO postgres;
GRANT ALL ON SCHEMA public TO public;";

            await connection.ExecuteAsync(sql);
        }

        bool runMigrations = false;
        if (runMigrations)
        {
            var serviceProvider = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddPostgres()
                    .WithGlobalConnectionString(ConnectionHelper.GetConnectionString())
                    .ScanIn(Assembly.GetAssembly(typeof(ServerState))).For.Migrations())
                .Configure<RunnerOptions>(opt => { opt.Tags = new[] { "SONG" }; })
                .AddLogging(lb => lb.AddFluentMigratorConsole())
                .BuildServiceProvider(false);

            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                runner.MigrateUp();
            }
        }
    }

    [Test, Explicit]
    public async Task FreshSetup_MB()
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        string executingDirectory = Directory.GetCurrentDirectory();

        if (!ConnectionHelper.GetConnectionString().Contains("DATABASE=EMQ;", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Database name in the connstr must be 'EMQ'");
        }

        string mbDir = $@"C:/emq/musicbrainz/{Constants.ImportDateMusicBrainz}";
        Directory.CreateDirectory(mbDir);

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Mb()))
        {
            Directory.SetCurrentDirectory(executingDirectory);
            string mbQueriesDir = @"../../../../Queries/MusicBrainz";

            // order is important
            var queryNames = new List<string>()
            {
                "aaa_rids.sql",
                "aaa_novgmdb.sql",
                "aaa_rec_vocals.sql",
                // "aaa_rec_lyricist.sql",
                "musicbrainz.sql",
                // "musicbrainz_release_recording.sql",
                "musicbrainz_vndb_artist.sql",
            };

            foreach (string filePath in queryNames.Select(x => Path.Combine(mbQueriesDir, x)))
            {
                string filename = Path.GetFileNameWithoutExtension(filePath);
                string sql = await File.ReadAllTextAsync(filePath);
                Console.WriteLine(
                    $"StartSection running query: {filename}: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

                var queryResult = (await connection.QueryAsync<string>(sql, commandTimeout: 1000)).ToList();
                foreach (var o in queryResult)
                {
                    Console.WriteLine(((string)(o.ToString())).Length);
                }

                if (queryResult.Any())
                {
                    await File.WriteAllTextAsync($"{mbDir}/{filename}.json", queryResult.Single());
                }
            }
        }

        // doesn't really work unless we delete unimported releases later
        // await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        // {
        //     Console.WriteLine(
        //         $"StartSection import musicbrainz_release_recording: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        //
        //     var json = JsonSerializer.Deserialize<MusicBrainzReleaseRecording[]>(
        //         await File.ReadAllTextAsync($"{mbDir}/musicbrainz_release_recording.json"))!;
        //     foreach (MusicBrainzReleaseRecording musicBrainzReleaseRecording in json)
        //     {
        //         await DbManager.InsertMusicBrainzReleaseRecording(musicBrainzReleaseRecording);
        //     }
        // }

        stopWatch.Stop();
        Console.WriteLine(
            $"StartSection finished: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
    }

    // [Test, Explicit]
    // public async Task ScaffoldQuizSongHistory()
    // {
    //     var regex = new Regex(@"SongHistory_(.+)_r(.+)q(.+)\.json",
    //         RegexOptions.Compiled | RegexOptions.CultureInvariant);
    //     string dir = @"C:\Users\Mert\Desktop\SongHistory";
    //     var jsons = Directory.EnumerateFiles(dir, "*.json");
    //     foreach (string json in jsons)
    //     {
    //         var match = regex.Match(json);
    //         string roomName = match.Groups[1].Value;
    //         Guid roomId = Guid.Parse(match.Groups[2].Value);
    //         Guid quizId = Guid.Parse(match.Groups[3].Value);
    //         var date = File.GetLastWriteTime(json);
    //
    //         string contents = await File.ReadAllTextAsync(json);
    //         var songHistories = JsonSerializer.Deserialize<Dictionary<int, SongHistory>>(contents, Utils.JsoIndented)!;
    //
    //         try
    //         {
    //             var entityRoom = new EntityRoom
    //             {
    //                 id = roomId, initial_name = roomName, created_by = 1, created_at = date
    //             };
    //             await DbManager.InsertEntity(entityRoom);
    //         }
    //         catch (Exception)
    //         {
    //             // ignored
    //         }
    //
    //         try
    //         {
    //             var entityQuiz = new EntityQuiz
    //             {
    //                 id = quizId,
    //                 room_id = roomId,
    //                 settings_b64 = "",
    //                 should_update_stats = true, // todo?
    //                 created_at = date,
    //             };
    //             long _ = await DbManager.InsertEntity(entityQuiz);
    //         }
    //         catch (Exception)
    //         {
    //             // ignored
    //         }
    //
    //         var quizSongHistories = new List<QuizSongHistory>();
    //         foreach ((int sp, SongHistory? songHistory) in songHistories)
    //         {
    //             foreach ((int userId, GuessInfo guessInfo) in songHistory.PlayerGuessInfos)
    //             {
    //                 var quizSongHistory = new QuizSongHistory
    //                 {
    //                     quiz_id = quizId,
    //                     sp = sp,
    //                     music_id = songHistory.Song.Id,
    //                     user_id = userId,
    //                     guess = guessInfo.Guess,
    //                     first_guess_ms = guessInfo.FirstGuessMs,
    //                     is_correct = guessInfo.IsGuessCorrect,
    //                     is_on_list = guessInfo.Labels?.Any() ?? false,
    //                     played_at = date,
    //                 };
    //
    //                 quizSongHistories.Add(quizSongHistory);
    //             }
    //         }
    //
    //         // Console.WriteLine(JsonSerializer.Serialize(quizSongHistories, Utils.JsoIndented));
    //
    //         if (!quizSongHistories.Any())
    //         {
    //             continue;
    //         }
    //
    //         try
    //         {
    //             bool success = await DbManager.InsertEntityBulk(quizSongHistories);
    //             if (!success)
    //             {
    //                 throw new Exception();
    //             }
    //         }
    //         catch (Exception e)
    //         {
    //             Console.WriteLine("Failed to insert QuizSongHistory");
    //             Console.WriteLine(e);
    //         }
    //
    //         await DbManager.RecalculateSongStats(songHistories.Select(x => x.Value.Song.Id).ToHashSet());
    //     }
    // }

    [Test, Explicit]
    public async Task CalculateAvgSongsPerVn()
    {
        // @formatter:off
        List<int> userIds = new() {2,4,5,9,10,11,16,18,20,21,22,25,27,30,31,32,33,35,38,39,40,42,51,52,54,55,63,66,72,82,92,94,104,111,113,117,123,124,126,134,136,138,147,185,187,};
        // @formatter:on

        string sql = @$"select user_id, json_agg(ulv.vnid) from users_label_vn ulv
join users_label ul on ul.id = ulv.users_label_id
where ul.kind = 1
and user_id < {Constants.PlayerIdGuestMin}
and user_id = ANY(@userIds)
group by user_id
order by user_id";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        Dictionary<int, List<string>> dict =
            (await connection.QueryAsync<(int uid, string urls)>(sql, new { userIds })).ToDictionary(
                x => x.uid,
                x => JsonSerializer.Deserialize<List<string>>(x.urls))!;

        foreach ((int key, List<string>? value) in dict)
        {
            dict[key] = value.Distinct().ToList();
        }

        var usernamesDict = (await connection.QueryAsync<(int, string)>("select id, username from users"))
            .ToDictionary(x => x.Item1, x => x.Item2);

        var filters = new QuizFilters()
        {
            CategoryFilters = new List<CategoryFilter>(),
            ArtistFilters = new List<ArtistFilter>(),
            VndbAdvsearchFilter = "",
            SongSourceSongTypeFilters =
                new Dictionary<SongSourceSongType, IntWrapper>
                {
                    { SongSourceSongType.OP, new IntWrapper(int.MaxValue) },
                    { SongSourceSongType.ED, new IntWrapper(int.MaxValue) },
                    { SongSourceSongType.Insert, new IntWrapper(int.MaxValue) },
                },
            SongSourceSongTypeRandomEnabledSongTypes = new Dictionary<SongSourceSongType, bool>(),
            SongDifficultyLevelFilters = Enum.GetValues<SongDifficultyLevel>().ToDictionary(x => x, _ => true),
            StartDateFilter = DateTime.Parse(Constants.QFDateMin, CultureInfo.InvariantCulture),
            EndDateFilter = DateTime.Parse(Constants.QFDateMax, CultureInfo.InvariantCulture),
            RatingAverageStart = Constants.QFRatingAverageMin,
            RatingAverageEnd = Constants.QFRatingAverageMax,
            RatingBayesianStart = Constants.QFRatingBayesianMin,
            RatingBayesianEnd = Constants.QFRatingBayesianMax,
            VoteCountStart = Constants.QFVoteCountMin,
            VoteCountEnd = Constants.QFVoteCountMax,
            OnlyOwnUploads = false,
            VNOLangs = Enum.GetValues<Language>().ToDictionary(x => x, _ => true),
        };

        List<string> res = new();
        foreach ((int key, List<string> value) in dict)
        {
            var songs = await DbManager.GetRandomSongs(int.MaxValue, true, value, filters);
            string str =
                $"{key}\t{usernamesDict[key]}\t{songs.Count}\t{value.Count}\t{(float)songs.Count / value.Count}";
            res.Add(str);
        }

        foreach (string re in res)
        {
            Console.WriteLine(re);
        }

        await File.WriteAllLinesAsync("deleteme.tsv", res);

        var res2 = new Dictionary<string, int>();
        foreach ((int key, List<string> value) in dict)
        {
            foreach (string s in value)
            {
                var songs = await DbManager.GetRandomSongs(int.MaxValue, true, new List<string> { s }, filters);
                if (!songs.Any())
                {
                    if (!res2.TryGetValue(usernamesDict[key], out _))
                    {
                        res2[usernamesDict[key]] = 0;
                    }

                    res2[usernamesDict[key]]++;
                }
            }
        }

        foreach ((string? key, int value) in res2)
        {
            Console.WriteLine($"{key}: {value}");
        }

        await File.WriteAllLinesAsync("deleteme_vnswith0songs.tsv", res);
    }

    [Test, Explicit]
    public static async Task DeleteOrphanedSelfHostStorageFiles()
    {
        var deletableFiles = new List<ISftpFile>();
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        string[] validFilenames =
            (await connection.QueryAsync<string>("select url from music_external_link where type = 2"))
            .Select(x => x.Replace("https://emqselfhost/selfhoststorage/", "")
                .Replace("https://erogemusicquiz.com/selfhoststorage/", "")
                .Replace("catbox/", "")
                .Replace("userup/", "")
                .Replace("weba/", "")).ToArray();

        var connectionInfo =
            new Renci.SshNet.ConnectionInfo(UploadConstants.SftpHost, UploadConstants.SftpUsername,
                new PasswordAuthenticationMethod(UploadConstants.SftpUsername, UploadConstants.SftpPassword));
        using (var client = new SftpClient(connectionInfo))
        {
            client.Connect();

            var files = client.ListDirectory(UploadConstants.SftpUserUploadDir);
            foreach (ISftpFile file in files)
            {
                bool olderThan14Days = (DateTime.UtcNow - file.LastWriteTimeUtc) > TimeSpan.FromDays(14);
                if (file.Name != ".." && olderThan14Days && !validFilenames.Contains(file.Name))
                {
                    deletableFiles.Add(file);
                }
            }

            client.Disconnect();
        }

        Console.WriteLine(
            $"{deletableFiles.Count} files ({(float)deletableFiles.Sum(x => x.Length) / 1024 / 1024 / 1024} GB)");

        foreach (ISftpFile deletableFile in deletableFiles)
        {
            Console.WriteLine(deletableFile.FullName);
        }

        // todo delete
    }

    [Test, Explicit]
    public static async Task RecalculateShouldUpdateStats()
    {
        const string sql = @"select id, settings_b64, should_update_stats from quiz";
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var res = await connection.QueryAsync<(Guid, string, bool)>(sql);
        foreach ((var quizId, string settingsB64, bool oldShouldUpdateStats) in res)
        {
            var deser = settingsB64.DeserializeFromBase64String_PB<QuizSettings>();
            bool newShouldUpdateStats = deser.ShouldUpdateStats;
            if (newShouldUpdateStats != oldShouldUpdateStats)
            {
                Console.WriteLine($"{quizId}: {oldShouldUpdateStats} => {newShouldUpdateStats}");
                await connection.ExecuteAsync(
                    @"UPDATE quiz set should_update_stats = @newShouldUpdateStats where id = @quizId",
                    new { newShouldUpdateStats, quizId });
            }
        }
    }

    [Test, Explicit]
    public static async Task RecalculateAllSongStats()
    {
        int count = await DbManager.SelectCountUnsafe("music");
        await DbManager.RecalculateSongStats(Enumerable.Range(1, count).ToHashSet());
    }

    [Test, Explicit]
    public static async Task PopulateMusicVote()
    {
        if (ConnectionHelper.GetConnectionString().Contains("erogemusicquiz.com"))
        {
            throw new Exception("wrong db");
        }

        const int numPlayers = 1_000;
        const int votesPerPlayer = 200;
        int count = await DbManager.SelectCountUnsafe("music");
        for (int i = 1; i <= numPlayers; i++)
        {
            for (int j = 1; j <= votesPerPlayer; j++)
            {
                var musicVote = new MusicVote
                {
                    music_id = Random.Shared.Next(1, count),
                    user_id = Random.Shared.Next(1, numPlayers),
                    vote = (short?)Random.Shared.Next(10, 100),
                    updated_at = DateTime.UtcNow
                };
                try
                {
                    await DbManager.UpsertEntity(musicVote);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }

    // [Test, Explicit]
    // public static async Task MigrateUserQuizSettings_OnlyFromListsAndBalancedStrictAndSongSourceSongTypeFiltersSumAndStartDateFilter()
    // {
    //     await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
    //     await connection.OpenAsync();
    //     await using var transaction = await connection.BeginTransactionAsync();
    //
    //     var userQuizSettings =
    //         (await connection.QueryAsync<UserQuizSettings>("select * from users_quiz_settings", transaction)).ToArray();
    //     foreach (UserQuizSettings uqs in userQuizSettings)
    //     {
    //         var deser = uqs.b64.DeserializeFromBase64String_PB<QuizSettings>();
    //         Console.Write($"{uqs.name.PadRight(32)} {uqs.b64.Length}");
    //
    //         int action = deser.OnlyFromLists ? deser.ListDistributionKind == ListDistributionKind.Unread ? 2 : 1 : 0;
    //         deser.Filters.ListReadKindFilters = action switch
    //         {
    //             0 => // !OnlyFromLists
    //                 new Dictionary<ListReadKind, IntWrapper>
    //                 {
    //                     { ListReadKind.Read, new IntWrapper(0) },
    //                     { ListReadKind.Unread, new IntWrapper(0) },
    //                     { ListReadKind.Random, new IntWrapper(deser.NumSongs) },
    //                 },
    //             1 => // OnlyFromLists
    //                 new Dictionary<ListReadKind, IntWrapper>
    //                 {
    //                     { ListReadKind.Read, new IntWrapper(deser.NumSongs) },
    //                     { ListReadKind.Unread, new IntWrapper(0) },
    //                     { ListReadKind.Random, new IntWrapper(0) },
    //                 },
    //             2 => // OnlyFromLists + Unread
    //                 new Dictionary<ListReadKind, IntWrapper>
    //                 {
    //                     { ListReadKind.Read, new IntWrapper(0) },
    //                     { ListReadKind.Unread, new IntWrapper(deser.NumSongs) },
    //                     { ListReadKind.Random, new IntWrapper(0) },
    //                 },
    //             _ => throw new UnreachableException()
    //         };
    //
    //         if (deser.SongSourceSongTypeFiltersSum != deser.NumSongs)
    //         {
    //             (SongSourceSongType key, IntWrapper? value) =
    //                 deser.Filters.SongSourceSongTypeFilters.MaxBy(x => x.Value.Value);
    //             deser.Filters.SongSourceSongTypeFilters[key].Value +=
    //                 deser.NumSongs - deser.SongSourceSongTypeFiltersSum;
    //         }
    //
    //         if (deser.ListDistributionKind == ListDistributionKind.BalancedStrict)
    //         {
    //             deser.ListDistributionKind = ListDistributionKind.Balanced;
    //         }
    //
    //         if (deser.Filters.StartDateFilter ==
    //             DateTime.ParseExact("1988-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture))
    //         {
    //             deser.Filters.StartDateFilter =
    //                 DateTime.ParseExact("1987-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
    //         }
    //
    //         Console.Write(" => ");
    //         uqs.b64 = deser.SerializeToBase64String_PB();
    //         Console.WriteLine(uqs.b64.Length);
    //     }
    //
    //     bool success = await connection.UpdateListAsync(userQuizSettings, transaction);
    //     if (success)
    //     {
    //         await transaction.CommitAsync();
    //     }
    //     else
    //     {
    //         throw new Exception();
    //     }
    // }

    [Test, Explicit]
    public async Task MergeDuplicateArtistsAddedToTheSameSong()
    {
        var dupeArtists = new HashSet<(int aId1, int aid2)>();
        var songs = await DbManager.GetRandomSongs(int.MaxValue, true);
        foreach (Song song in songs)
        {
            var seenArtists = new Dictionary<string, List<int>>();
            foreach (SongArtist songArtist in song.Artists)
            {
                if (songArtist.Titles.Count > 1)
                {
                    // idk some weird stuff like quinrose & mari
                    Console.WriteLine(
                        $"songArtist.Titles.Count > 1 {JsonSerializer.Serialize(songArtist.Titles, Utils.JsoIndented)}");
                    continue;
                }

                string norm = songArtist.Titles.Single().LatinTitle.NormalizeForAutocomplete();
                if (seenArtists.TryGetValue(norm, out var aIds))
                {
                    // Console.WriteLine($"dupe artist: mId {song.Id} {norm}");
                    aIds.Add(songArtist.Id);
                }
                else
                {
                    seenArtists[norm] = new List<int> { songArtist.Id };
                }
            }

            foreach ((string _, List<int>? value) in seenArtists)
            {
                switch (value.Count)
                {
                    case 2:
                        dupeArtists.Add((value[0], value[1]));
                        break;
                    case > 2:
                        Console.WriteLine(">2");
                        break;
                }
            }
        }

        foreach ((int aId1, int aId2) in dupeArtists.OrderBy(x => x.aId1).ThenBy(y => y.aid2))
        {
            // Console.WriteLine($"{aId1} <=> {aId2}");
            var actionResult = await ServerUtils.BotEditMergeArtists(new MergeArtists { SourceId = aId1, Id = aId2 });
            if (actionResult is not OkResult)
            {
                var badRequestObjectResult = actionResult as BadRequestObjectResult;
                Console.WriteLine($"actionResult is not OkResult: {aId1} {aId2} {badRequestObjectResult?.Value}");
            }
        }
    }

    [Test, Explicit]
    public async Task MergeMBArtistsWithoutVNDBLinks()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var vndbArtistsWithoutMb = (await connection.QueryAsync<(int, string[])>(@"
SELECT ael.artist_id, array_agg(DISTINCT aa.latin_alias) FROM artist_external_link ael
JOIN artist_alias aa ON aa.artist_id = ael.artist_id
WHERE ael.artist_id NOT IN (SELECT artist_id FROM artist_external_link ael2 WHERE TYPE = 2)
AND ael.artist_id IN (SELECT artist_id FROM artist_external_link ael2 WHERE TYPE = 1)
GROUP BY ael.artist_id
HAVING array_length(array_agg(DISTINCT aa.latin_alias), 1) = 1 --todo
")).Select(x => (x.Item1, x.Item2.Single().NormalizeForAutocomplete())).ToArray();

        var mbArtistsWithoutVndb = (await connection.QueryAsync<(int, string[])>(@"
SELECT ael.artist_id, array_agg(DISTINCT aa.latin_alias) FROM artist_external_link ael
JOIN artist_alias aa ON aa.artist_id = ael.artist_id
WHERE ael.artist_id IN (SELECT artist_id FROM artist_external_link ael2 WHERE TYPE = 2)
AND ael.artist_id NOT IN (SELECT artist_id FROM artist_external_link ael2 WHERE TYPE = 1)
GROUP BY ael.artist_id
HAVING array_length(array_agg(DISTINCT aa.latin_alias), 1) = 1
")).Select(x => (x.Item1, x.Item2.Single().NormalizeForAutocomplete())).ToArray();

        foreach ((int aid, string latinTitle) in mbArtistsWithoutVndb)
        {
            if (Setup.BlacklistedCreaterNames.Contains(latinTitle.ToLowerInvariant()))
            {
                continue;
            }

            var target = vndbArtistsWithoutMb.FirstOrDefault(x => x.Item2 == latinTitle);
            if (target.Item1 > 0)
            {
                var actionResult = await ServerUtils.BotEditMergeArtists(new MergeArtists
                {
                    SourceName = latinTitle, SourceId = aid, Id = target.Item1
                });
                if (actionResult is not OkResult)
                {
                    var badRequestObjectResult = actionResult as BadRequestObjectResult;
                    Console.WriteLine(
                        $"actionResult is not OkResult: {aid} {target.Item1} {badRequestObjectResult?.Value}");
                }
            }
        }
    }

    [Test, Explicit]
    public async Task RemoveStatsFromEntityQueueJson()
    {
        var regex = new Regex(@"""Stats"":{.+?},""MusicBrainzReleases", RegexOptions.Compiled);
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var editQueues = (await connection.GetListAsync<EditQueue>()).ToArray();
        foreach (EditQueue editQueue in editQueues)
        {
            editQueue.entity_json = regex.Replace(editQueue.entity_json, @"""Stats"":null,""MusicBrainzReleases");
            if (editQueue.old_entity_json != null)
            {
                editQueue.old_entity_json =
                    regex.Replace(editQueue.old_entity_json, @"""Stats"":null,""MusicBrainzReleases");
            }
        }

        await connection.UpsertListAsync(editQueues);
    }

    [Test, Explicit]
    public async Task ScaffoldQuizSongHistoryScalingTest()
    {
        // const int roomCount = 5000; // * 1800 v
        // const int quizzesPerRoom = 5;
        // const int songsPerQuiz = 40;
        // const int usersPerRoom = 3;
        // const int guessKindsPerUser = 3;

        const int roomCount = 100; // * 60000 v
        const int quizzesPerRoom = 10;
        const int songsPerQuiz = 100;
        const int usersPerRoom = 10;
        const int guessKindsPerUser = 6;

        const string roomName = "Room";
        var date = DateTime.UtcNow;
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var rooms = Enumerable.Range(1, roomCount).Select(x => new EntityRoom
        {
            id = Guid.NewGuid(), initial_name = roomName, created_by = 1, created_at = date
        }).ToArray();

        var quizzes = new List<EntityQuiz>(roomCount * quizzesPerRoom);
        var quizSongHistories = new List<QuizSongHistory>();
        foreach (EntityRoom entityRoom in rooms)
        {
            for (int i = 0; i < quizzesPerRoom; i++)
            {
                var quiz = new EntityQuiz
                {
                    id = Guid.NewGuid(),
                    room_id = entityRoom.id,
                    settings_b64 = "",
                    should_update_stats = true,
                    created_at = date,
                };
                quizzes.Add(quiz);

                for (int sp = 0; sp < songsPerQuiz; sp++)
                {
                    for (int userId = 0; userId < usersPerRoom; userId++)
                    {
                        for (int guessKind = 0; guessKind < guessKindsPerUser; guessKind++)
                        {
                            var quizSongHistory = new QuizSongHistory
                            {
                                quiz_id = quiz.id,
                                sp = sp,
                                music_id = sp + 4,
                                user_id = userId + 1500,
                                guess_kind = (GuessKind)guessKind,
                                guess = "my test guess",
                                first_guess_ms = 7000,
                                is_correct = false,
                                is_on_list = false,
                                played_at = date,
                                // start_time = , // todo?
                                // duration = , // todo?
                            };

                            quizSongHistories.Add(quizSongHistory);
                        }
                    }
                }
            }
        }

        await connection.InsertListAsync(rooms);
        await connection.InsertListAsync(quizzes);
        await connection.InsertListAsync(quizSongHistories);
        await transaction.CommitAsync();
        // await DbManager.RecalculateSongStats(songHistories.Select(x => x.Value.Song.Id).ToHashSet());
    }

    [Test, Explicit]
    public async Task ListArtistCommaAliases()
    {
        bool onlyMainTitles = true;
        string[] blacklist = { "ltd", "inc.", "co.", };
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());

        var artistIds = await connection.QueryAsync<int>("select id from artist");
        var songs = artistIds.Select(x => new Song { Artists = new List<SongArtist> { new() { Id = x } } }).ToList();
        var artists = (await DbManager.SelectArtistBatchNoAM(connection, songs, false))
            .SelectMany(x => x.Value.Select(y => y.Value)).DistinctBy(x => x.Id).ToArray();

        foreach (SongArtist artist in artists)
        {
            var commaTitle = artist.Titles.FirstOrDefault(x =>
                x.LatinTitle.Contains(',') &&
                (!onlyMainTitles || x.IsMainTitle || !artist.Titles.Any(y => y.IsMainTitle)) &&
                !blacklist.Any(y => x.LatinTitle.Contains(y, StringComparison.OrdinalIgnoreCase)));
            if (commaTitle != null)
            {
                Console.WriteLine(commaTitle.LatinTitle);
            }
        }
    }

    [Test, Explicit]
    public async Task ListArtistsWithoutMainAlias()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());

        var artistIds = await connection.QueryAsync<int>("select id from artist");
        var songs = artistIds.Select(x => new Song { Artists = new List<SongArtist> { new() { Id = x } } }).ToList();
        var artists = (await DbManager.SelectArtistBatchNoAM(connection, songs, false))
            .SelectMany(x => x.Value.Select(y => y.Value)).DistinctBy(x => x.Id).ToArray();

        foreach (SongArtist artist in artists)
        {
            if (artist.Titles.Count > 1 && !artist.Titles.Any(x => x.IsMainTitle) &&
                artist.Links.Any(x => x.Type == SongArtistLinkType.VNDBStaff))
            {
                Console.WriteLine(artist.Titles.FirstOrDefault(x => x.LatinTitle.Contains(','))?.LatinTitle ??
                                  artist.Titles.First().LatinTitle);
            }
        }
    }

    // [Test, Explicit]
    // public async Task MergeArtistCommaAliases()
    // {
    //     await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
    //     await connection.OpenAsync();
    //     await using var transaction = await connection.BeginTransactionAsync();
    //
    //     var artistIds = await connection.QueryAsync<int>("select id from artist");
    //     var songs = artistIds.Select(x => new Song { Artists = new List<SongArtist> { new() { Id = x } } }).ToList();
    //     var artists = (await DbManager.SelectArtistBatchNoAM(connection, songs, false))
    //         .SelectMany(x => x.Value.Select(y => y.Value)).DistinctBy(x => x.Id).ToArray();
    //
    //     foreach (SongArtist artist in artists)
    //     {
    //         // todo search reverse name order
    //         // todo romanization differences etc
    //         var commaTitle = artist.Titles.FirstOrDefault(x => x.LatinTitle.Contains(','));
    //         if (commaTitle != null)
    //         {
    //             var nonCommaTitle =
    //                 artist.Titles.FirstOrDefault(x => x.LatinTitle == commaTitle.LatinTitle.Replace(",", ""));
    //             if (nonCommaTitle != null)
    //             {
    //                 Console.WriteLine(nonCommaTitle.LatinTitle);
    //                 // todo update am rows using comma alias to non-comma alias
    //                 // todo delete am row with comma alias
    //             }
    //         }
    //     }
    //
    //     // await transaction.CommitAsync();
    // }

    [Test, Explicit]
    public async Task MergeArtistsMbVndbLinks()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionMb = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Mb());
        var batch = await connectionMb.QueryAsync<(string, string)>(
            @"SELECT 'https://musicbrainz.org/artist/'||a.gid, url.url FROM url
        JOIN l_artist_url lau ON lau.entity1 = url.id
        JOIN artist a ON a.id = lau.entity0
        WHERE url like '%https://vndb.org/s%'");

        var aels = (await connection.GetListAsync<ArtistExternalLink>()).ToArray();
        foreach ((string? item1, string? item2) in batch)
        {
            int[] aids = aels.Where(x => x.url == item1 || x.url == item2)
                .Select(x => x.artist_id).Distinct().ToArray();
            switch (aids.Length)
            {
                case < 2:
                    // Console.WriteLine($"<2: {aids.Length} {item1} {item2}");
                    if (aids.Length == 0)
                    {
                        Console.WriteLine($"unlinked: {item1} {item2}");
                        // todo insert
                    }

                    break;
                case 2:
                    var actionResult =
                        await ServerUtils.BotEditMergeArtists(new MergeArtists { SourceId = aids[0], Id = aids[1] });
                    if (actionResult is not OkResult)
                    {
                        var badRequestObjectResult = actionResult as BadRequestObjectResult;
                        Console.WriteLine(
                            $"actionResult is not OkResult: {aids[0]} {aids[1]} {badRequestObjectResult?.Value}");
                    }

                    break;
                case > 2:
                    Console.WriteLine($">2: {item1} {item2}");
                    break;
            }
        }
    }

    [Test, Explicit]
    public async Task ListArtistAliasesThatDifferOnlyInIsMainAliasFlag()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var aas = (await connection.GetListAsync<ArtistAlias>()).ToArray();
        foreach (ArtistAlias aa in aas)
        {
            var dup = aas.Where(x =>
                x.artist_id == aa.artist_id &&
                x.latin_alias == aa.latin_alias &&
                x.non_latin_alias == aa.non_latin_alias).ToList();
            if (dup.Count > 1)
            {
                Console.WriteLine(JsonSerializer.Serialize(dup, Utils.JsoIndented));
                Console.WriteLine("-------------------------------------------------------");
            }
        }
    }

    [Test, Explicit]
    public async Task CalculateSizeOfLinks()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var links = await connection.QueryAsync<string?>(
            $@"select analysis_raw from music_external_link where type = {(int)SongLinkType.Self} and
 not is_video and music_id not in (select music_id from music_source_music where type = {(int)SongSourceSongType.BGM})");

        decimal total = 0;
        foreach (string? link in links)
        {
            if (link is null or "null")
            {
                Console.WriteLine("null");
                continue;
            }

            var d = JsonSerializer.Deserialize<MediaAnalyserResult>(link);
            if (d != null)
            {
                total += (decimal)(d.FilesizeMb ?? 0);
            }
        }

        Console.WriteLine(total / 1024);
    }

    [Test, Explicit]
    public static async Task Multirangetest()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        // var newRanges = new TimeRange[] { new(1.05, 16.77), new(24.31, 85.21) };
        //
        // await connection.ExecuteAsync(
        //     "update music_external_link SET vocals_ranges = @newRanges WHERE url LIKE '%00082bdf-930e-4dd3-9246-0d2095cf4f3a%'",
        //     new { newRanges });

        var results =
            await connection.QueryAsync<TimeRange[]>(
                "SELECT vocals_ranges FROM music_external_link where vocals_ranges is not null");

        results =
            await connection.QueryAsync<TimeRange[]>(
                @"SELECT
        multirange_minus(
            tsmultirange(tsrange('[1970-01-01 00:00:00, infinity)')),
            vocals_ranges
        ) AS free_times
        FROM music_external_link mel
            WHERE vocals_ranges IS NOT null");
        foreach (TimeRange[] timeRanges in results)
        {
            foreach (TimeRange timeRange in timeRanges)
            {
                Console.WriteLine(timeRange);
            }
        }
    }

    [Test, Explicit]
    public static async Task InsertVocalsRangesSharp()
    {
        const string baseOutputPath = @"O:\!demucsoutput\htdemucs";
        string[] files = Directory.GetFiles(baseOutputPath, "vocals.flac", SearchOption.AllDirectories).OrderBy(x => x)
            .ToArray();

        const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
        Dictionary<int, HashSet<SongSourceSongType>> mids =
            (await new NpgsqlConnection(ConnectionHelper.GetConnectionString()).QueryAsync<(int, int)>(sqlMids))
            .GroupBy(x => x.Item1)
            .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

        List<int> validMids = mids
            .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
            .Select(z => z.Key)
            .ToList();

        List<Song> dbSongs = await DbManager.SelectSongsMIds(validMids.ToArray(), false);
        var validUrls = dbSongs.SelectMany(x =>
                x.Links.Where(y => y.IsFileLink && !y.IsVideo && !y.VocalsRanges.Any())
                    .Select(y => y.Url.ReplaceSelfhostLink().LastSegment()
                        .Replace(".weba", "").Replace(".mp3", "")
                        .Replace(".ogg", "").Replace(".flac", "")))
            .ToHashSet();

        files = files.Where(x => validUrls.Any(y => x.Contains(y))).ToArray();
        var options = new VocalDetectorOptions { EnergyThreshold = 0.1, MinSilenceDurationSec = 3, };
        await Parallel.ForEachAsync(files,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
            async (file, _) =>
            {
                var ranges = VocalDetector.Detect(file, options).Where(x => x.Duration > 3);
                await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                int rows = await connection.ExecuteAsync(
                    "update music_external_link SET vocals_ranges = @ranges WHERE url LIKE @url",
                    new
                    {
                        ranges = ranges.ToArray(),
                        url = $"%{new DirectoryInfo(file).Parent!.Name.Replace(".weba", "")}%"
                    });
                switch (rows)
                {
                    case <= 0:
                        Console.WriteLine($"not found in db: {file}");
                        break;
                    case >= 3:
                        Console.WriteLine($"rows >= 3: {file}");
                        break;
                }
            });
    }

    [Test, Explicit]
    public static async Task CopyToDemucsInputFolder()
    {
        const string baseDownloadDir = "K:/emq/emqsongsbackup2";
        const string baseOutputDir = "G:/!demucsinput";
        // Directory.SetCurrentDirectory(baseDownloadDir);
        string[] filePaths = Directory.GetFiles(baseDownloadDir, "*.*",
            new EnumerationOptions() { RecurseSubdirectories = true });

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
        Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
            .GroupBy(x => x.Item1)
            .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

        List<int> validMids = mids
            .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
            .Select(z => z.Key)
            .ToList();

        List<Song> dbSongs = await DbManager.SelectSongsMIds(validMids.ToArray(), false);
        var validUrls = dbSongs.SelectMany(x =>
                x.Links.Where(y => y.IsFileLink && !y.IsVideo).Select(y => y.Url.ReplaceSelfhostLink().LastSegment()))
            .ToHashSet();
        foreach (string filePath in filePaths)
        {
            string fileName = Path.GetFileName(filePath);
            if (validUrls.Contains(fileName))
            {
                // Console.WriteLine(fileName);
                var fileInfo = new FileInfo(fileName);
                bool isWeba = fileInfo.Extension == ".weba";
                string finalFilename = $"{baseOutputDir}/{(isWeba ? $"{fileInfo.Name}.flac" : fileName)}";
                if (File.Exists(finalFilename))
                {
                    continue;
                }

                if (isWeba)
                {
                    string outputFinal = $"{Path.GetTempFileName()}.flac";
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i \"{filePath}\" -nostdin \"{outputFinal}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    string err = await process.StandardError.ReadToEndAsync();
                    if (File.Exists(outputFinal))
                    {
                        File.Move(outputFinal, finalFilename);
                    }
                    else
                    {
                        Console.WriteLine($"error transcoding to flac: {err}");
                    }
                }
                else
                {
                    File.Copy(filePath, finalFilename);
                }
            }
        }
    }

    [Test, Explicit]
    public static async Task DetectPossibleUpscale()
    {
    }

    [Test, Explicit]
    public static async Task GenerateAutocompleteShortcuts()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());

        var artistIds = await connection.QueryAsync<int>("select id from artist");
        var songs = artistIds.Select(x => new Song { Artists = new List<SongArtist> { new() { Id = x } } }).ToList();
        var artists = (await DbManager.SelectArtistBatchNoAM(connection, songs, false))
            .SelectMany(x => x.Value.Select(y => y.Value)).DistinctBy(x => x.Id).ToArray();

        var comp = new AutocompleteAComponent
        {
            AutocompleteData = JsonSerializer.Deserialize<AutocompleteA[]>(
                await File.ReadAllTextAsync(@"a.json"))!
        };

        foreach (SongArtist artist in artists)
        {
            const int minLength = 3;
            const int maxLength = 10;
            string? latinTitle = artist.Titles.FirstOrDefault(x => x.IsMainTitle)?.LatinTitle;
            if (latinTitle is null)
            {
                continue;
            }

            string input = latinTitle.NormalizeForAutocomplete();
            if (string.IsNullOrEmpty(input) || input.Length < minLength)
                continue;

            // Ensure maxLength doesn't exceed string length
            int actualMaxLength = Math.Min(maxLength, input.Length);

            // Validate length constraints
            if (minLength > actualMaxLength)
                continue;

            Console.WriteLine(input);
            var substrings = new List<string>(128);
            for (int start = 0; start < input.Length; start++)
            {
                // Calculate the minimum end position for this start
                int minEnd = start + minLength;
                if (minEnd > input.Length) break;

                // Calculate the maximum end position for this start
                int maxEnd = Math.Min(start + actualMaxLength, input.Length);

                for (int end = minEnd; end <= maxEnd; end++)
                {
                    substrings.Add(input[start..end]);
                }
            }

            input = Utils.GetReversedArtistName(latinTitle); // todo
            for (int start = 0; start < input.Length; start++)
            {
                // Calculate the minimum end position for this start
                int minEnd = start + minLength;
                if (minEnd > input.Length) break;

                // Calculate the maximum end position for this start
                int maxEnd = Math.Min(start + actualMaxLength, input.Length);

                for (int end = minEnd; end <= maxEnd; end++)
                {
                    substrings.Add(input[start..end]);
                }
            }

            var dict = new Dictionary<int, string>();
            foreach (string substring in substrings)
            {
                //Console.WriteLine(substring);
                var search = comp.OnSearch<AutocompleteA>(substring).ToList();
                var first = search.FirstOrDefault(x => x.AId == artist.Id);
                if (first != null)
                {
                    int pos = search.IndexOf(first);
                    if (pos is >= 0 and <= 8)
                    {
                        if (dict.TryGetValue(pos, out string? existing))
                        {
                            if (substring.Length >= existing.Length)
                            {
                                continue;
                            }
                        }

                        dict[pos] = substring;
                    }
                }
            }

            int shortestInt = maxLength;
            string shortestStr = "";
            foreach ((int _, string? value) in dict)
            {
                if (value.Length < shortestInt)
                {
                    shortestInt = value.Length;
                    shortestStr = value;
                }
            }

            int smallestInt = maxLength;
            string smallestStr = "";
            foreach ((int key, string? value) in dict)
            {
                if (key < smallestInt)
                {
                    smallestInt = key;
                    smallestStr = value;
                }
            }

            if (shortestStr.Length < smallestStr.Length)
            {
                Console.WriteLine($"{shortestStr} ({shortestInt})");
            }

            Console.WriteLine($"{smallestStr} ({smallestInt})");
            Console.WriteLine();
            //return;
        }
    }

    [Test, Explicit]
    public static async Task FillDevelopers()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        string[] withDevs = DbManager.VnDevelopers.Keys.Select(x => x.ToVndbUrl()).ToArray();
        int[] maybe = (await connection.QueryAsync<int>(
            $"select music_source_id from music_source_external_link msel join music_source ms on ms.id = msel.music_source_id where msel.type = {(int)SongSourceLinkType.VNDB} and not url=any(@withDevs) and developers is null",
            new { withDevs })).OrderBy(x => x).ToArray();
        foreach (int i in maybe)
        {
            Console.WriteLine($"https://erogemusicquiz.com/ems{i}");
        }
    }
}
