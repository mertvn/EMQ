using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.EGS;
using EMQ.Server.Db.Imports.MusicBrainz;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Npgsql;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

public class EntryPoints
{
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
    public async Task ImportMusicBrainzData()
    {
        await MusicBrainzImporter.ImportMusicBrainzData();
    }

    [Test, Explicit]
    public async Task GenerateSong()
    {
        await File.WriteAllTextAsync("Song.json", await DbManager.ExportSong());
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
                     (ReviewQueueStatus)x.status == ReviewQueueStatus.Approved))
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

    [Test, Explicit]
    public async Task ImportSongLite()
    {
        var deserialized =
            JsonConvert.DeserializeObject<List<SongLite>>(
                await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\SongLite.json"));
        await DbManager.ImportSongLite(deserialized!);
    }

    [Test, Explicit]
    public async Task ImportSongLite_MB()
    {
        var deserialized =
            JsonConvert.DeserializeObject<List<SongLite_MB>>(
                await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\SongLite_MB.json"));
        await DbManager.ImportSongLite_MB(deserialized!);
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
    public async Task AnalyzeReviewQueueItems()
    {
        bool deleteAfter = false;

        var rqs = await DbManager.FindRQs(DateTime.MinValue, DateTime.MaxValue);
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
        var rqs = await DbManager.FindRQs(DateTime.MinValue, DateTime.MaxValue);
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
    [Test, Explicit]
    public async Task BackupSongFilesUsingBothSongLites()
    {
        const string baseDownloadDir = "K:\\emq\\emqsongsbackup";
        Directory.CreateDirectory(baseDownloadDir);

        const string songLitePath = "C:\\emq\\emqsongsmetadata\\SongLite.json";
        var songLites =
            JsonSerializer.Deserialize<List<SongLite>>(await File.ReadAllTextAsync(songLitePath),
                Utils.JsoIndented)!;

        const string songLiteMbPath = "C:\\emq\\emqsongsmetadata\\SongLite_MB.json";
        var songLiteMbs =
            JsonSerializer.Deserialize<List<SongLite_MB>>(await File.ReadAllTextAsync(songLiteMbPath),
                Utils.JsoIndented)!;

        int dlCount = 0;
        var allLinks = songLites.SelectMany(x => x.Links).Concat(songLiteMbs.SelectMany(y => y.Links));
        foreach (var link in allLinks)
        {
            string filePath;
            switch (link.Type)
            {
                case SongLinkType.Catbox:
                    filePath = $"{baseDownloadDir}\\catbox\\{link.Url.LastSegment()}";
                    break;
                case SongLinkType.Self:
                    if (link.Url.Contains("catbox"))
                    {
                        // skip mirror links
                        continue;
                    }

                    filePath = $"{baseDownloadDir}\\selfhoststorage\\{link.Url.LastSegment()}";
                    break;
                case SongLinkType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!File.Exists(filePath))
            {
                bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(link.Url));
                if (success)
                {
                    dlCount += 1;
                    if (link.Type is SongLinkType.Catbox)
                    {
                        await Task.Delay(10000);
                    }
                }
                else
                {
                    return;
                }
            }
        }

        Console.WriteLine($"Downloaded {dlCount} files.");
    }

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
            foreach (SongLink dbSongLink in dbSong.Links)
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
            new Setup().RunBeforeTests();
            var vndbStaffNotesParserTests = new VNDBStaffNotesParserTests();
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
    public async Task PgRestore()
    {
        Directory.SetCurrentDirectory(@"C:/emq/dbbackups");
        string envVar = "DATABASE_URL";
        // envVar = "EMQ_AUTH_DATABASE_URL";

        var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
        Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

        string dumpFileName = "pgdump_2024-02-13_EMQ@localhost.tar";
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
        if (ConnectionHelper.GetConnectionString().Contains("railway"))
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

        throw new Exception();
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

        // todo
        string cnnstrMusicbrainz = ConnectionHelper
            .GetConnectionStringBuilderWithDatabaseUrl(
                "postgresql://musicbrainz:musicbrainz@192.168.56.101:5432/musicbrainz_db")
            .ToString();

        string mbDir = $@"C:/emq/musicbrainz/{Constants.ImportDateMusicBrainz:yyyy-MM-dd}";
        Directory.CreateDirectory(mbDir);

        await using (var connection = new NpgsqlConnection(cnnstrMusicbrainz))
        {
            Directory.SetCurrentDirectory(executingDirectory);
            string mbQueriesDir = @"../../../../Queries/MusicBrainz";

            // order is important
            var queryNames = new List<string>()
            {
                "aaa_rids.sql",
                "aaa_novgmdb.sql",
                "aaa_rec_vocals.sql",
                "aaa_rec_lyricist.sql",
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

    [Test, Explicit]
    public async Task ReplaceMergedRecordingsInSongLite_MB()
    {
        const string sqlRedirect = "SELECT new_id FROM recording_gid_redirect WHERE gid = @gid";
        const string sqlRecordingGid = "SELECT gid FROM recording WHERE id = @id";

        async Task<Guid> GetMergedRecordingGid(IDbConnection connection, Guid gid)
        {
            int newId = await connection.QuerySingleOrDefaultAsync<int>(sqlRedirect, new { gid = gid });
            if (newId > 0)
            {
                gid = await connection.QuerySingleAsync<Guid>(sqlRecordingGid, new { id = newId });
                await GetMergedRecordingGid(connection, gid);
            }

            return gid;
        }

        if (!ConnectionHelper.GetConnectionString().Contains("DATABASE=EMQ;", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Database name in the connstr must be 'EMQ'");
        }

        const string path = "C:\\emq\\emqsongsmetadata\\SongLite_MB.json";
        var deserialized = JsonConvert.DeserializeObject<List<SongLite_MB>>(await File.ReadAllTextAsync(path))!;
        File.Copy(path, $"{path}@{DateTime.UtcNow:yyyy-MM-ddTHH_mm_ss_fff}.json");

        // todo
        string cnnstrMusicbrainz = ConnectionHelper
            .GetConnectionStringBuilderWithDatabaseUrl(
                "postgresql://musicbrainz:musicbrainz@192.168.56.101:5432/musicbrainz_db")
            .ToString();

        await Parallel.ForEachAsync(deserialized,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 0 },
            async (songLite, _) =>
            {
                await using (var connection = new NpgsqlConnection(cnnstrMusicbrainz))
                {
                    Guid oldGid = songLite.Recording;
                    Guid newGid = await GetMergedRecordingGid(connection, oldGid);

                    if (oldGid != newGid)
                    {
                        Console.WriteLine($"{oldGid} => {newGid}");
                        songLite.Recording = newGid;
                    }
                }
            });

        deserialized = deserialized.DistinctBy(x => x.Recording).ToList();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(deserialized, Utils.JsoIndented));
    }
}
