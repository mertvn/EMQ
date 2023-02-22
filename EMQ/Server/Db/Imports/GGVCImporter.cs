using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using DapperQueryBuilder;
using EMQ.Shared.Core;
using Npgsql;

namespace EMQ.Server.Db.Imports;

public class GGVCImporter
{
    public static async Task ImportGGVC()
    {
        var regex = new Regex("【(.+)】(?: )?(.+)(?: )?(?:\\[|【)(.*)(?:]|】)", RegexOptions.Compiled);
        var ggvcSongs = new List<GGVCSong>();

        string dir = "M:\\[IMS][Galgame Vocal MP3 Collection 1996-2006]";
        // dir = "M:\\a";
        string[] subDirs = Directory.GetDirectories(dir);
        foreach (string subDir in subDirs)
        {
            // Console.WriteLine("start subDir " + subDir);
            string[] filePaths = Directory.GetFiles(subDir);
            foreach (string filePath in filePaths)
            {
                string fileName = Path.GetFileName(filePath);
                if (!fileName.StartsWith("【") ||
                    fileName.ToLowerInvariant().Contains("mix") ||
                    fileName.ToLowerInvariant().Contains("arrange") ||
                    fileName.ToLowerInvariant().Contains("アレンジ")||
                    fileName.ToLowerInvariant().Contains("acoustic")||
                    fileName.ToLowerInvariant().Contains("ver."))
                {
                    Console.WriteLine("skipping: " + fileName);
                    continue;
                }

                var match = regex.Match(fileName);
                // Console.WriteLine("match: " + match.ToString());

                if (string.IsNullOrWhiteSpace(match.ToString()))
                {
                    Console.WriteLine("regex didn't match: " + fileName);
                    // throw new Exception("didn't match: " + fileName);
                }

                string source = match.Groups[1].Value;
                string title = match.Groups[2].Value;
                string artist = match.Groups[3].Value;

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(title) ||
                    string.IsNullOrWhiteSpace(artist))
                {
                    continue;
                }

                source = source.Trim();
                title = title.Trim();
                artist = artist.Trim();

                List<string> sources = new() { source };
                List<string> titles = new() { title };
                List<string> artists = new() { artist };

                var tfile = TagLib.File.Create(filePath);
                string? metadataSources = tfile.Tag.Album;
                string? metadataTitle = tfile.Tag.Title;
                string[] metadataArtists = tfile.Tag.Performers.Concat(tfile.Tag.AlbumArtists).ToArray();

                if (!string.IsNullOrWhiteSpace(metadataSources))
                {
                    sources.Add(metadataSources);
                }

                if (!string.IsNullOrWhiteSpace(metadataTitle))
                {
                    titles.Add(metadataTitle);
                }

                artists.AddRange(metadataArtists.Where(x => !string.IsNullOrWhiteSpace(x)));

                sources = sources.Distinct().ToList();
                titles = titles.Distinct().ToList();
                artists = artists.Distinct().ToList();

                var ggvcSong = new GGVCSong
                {
                    Path = filePath, Sources = sources, Titles = titles, Artists = artists,
                };
                ggvcSongs.Add(ggvcSong);
            }
        }

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        }

        var ggvcInnerResults = new ConcurrentBag<GGVCInnerResult>();
        await Parallel.ForEachAsync(ggvcSongs, async (ggvcSong, _) =>
        {
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                var innerResult = new GGVCInnerResult() { GGVCSong = ggvcSong };
                ggvcInnerResults.Add(innerResult);

                // Console.WriteLine(JsonSerializer.Serialize(ggvcSong.Artists, Utils.Jso));
                var aIds = await DbManager.FindArtistIdsByArtistNames(ggvcSong.Artists);
                innerResult.aIds = aIds;

                if (!aIds.Any())
                {
                    innerResult.ResultKind = GGVCInnerResultKind.NoAids;
                    return;
                }

                var queryMusicSource = connection
                    .QueryBuilder($@"SELECT DISTINCT ms.id
FROM music m
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
LEFT JOIN music_source_title mst on ms.id = mst.music_source_id
/**where**/");

                queryMusicSource.Where($"mst.is_main_title = true");
                queryMusicSource.Where(
                    $"(mst.latin_title % ANY({ggvcSong.Sources}) OR mst.non_latin_title % ANY({ggvcSong.Sources}))");

                var msIds = (await queryMusicSource.QueryAsync<int>()).ToList();
                if (!msIds.Any())
                {
                    innerResult.ResultKind = GGVCInnerResultKind.NoSources;
                    return;
                }

                var queryMusic = connection
                    .QueryBuilder($@"SELECT DISTINCT m.id
FROM music m
LEFT JOIN music_title mt ON mt.music_id = m.id
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
LEFT JOIN music_source_title mst on ms.id = mst.music_source_id
LEFT JOIN artist_music am ON am.music_id = m.id
LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
LEFT JOIN artist a ON a.id = aa.artist_id
/**where**/");

                queryMusic.Where($"mst.is_main_title = true");
                queryMusic.Where(
                    $"ms.id = ANY({msIds})");
                queryMusic.Where(
                    $"(mt.latin_title % ANY({ggvcSong.Titles}) OR mt.non_latin_title % ANY({ggvcSong.Titles}))");
                queryMusic.Where(
                    $"a.id = ANY({aIds})");

                // Console.WriteLine(queryMusic.Sql);
                //Console.WriteLine(JsonSerializer.Serialize(queryMusic.Parameters, Utils.JsoIndented));

                var mids = (await queryMusic.QueryAsync<int>()).ToList();
                if (mids.Any())
                {
                    innerResult.mIds.AddRange(mids);
                    if (mids.Count > 1)
                    {
                        innerResult.ResultKind = GGVCInnerResultKind.MultipleMids;
                        return;
                    }

                    // todo check if we already have a song link here
                    innerResult.ResultKind = GGVCInnerResultKind.Matched;
                    return;
                }
                else
                {
                    innerResult.ResultKind = GGVCInnerResultKind.NoMids;
                    return;
                }
            }
        });

        foreach (var egsImporterInnerResult in ggvcInnerResults)
        {
            bool needsPrint;
            switch (egsImporterInnerResult.ResultKind)
            {
                case GGVCInnerResultKind.Matched:
                    Console.WriteLine("matched: ");
                    needsPrint = true;
                    break;
                case GGVCInnerResultKind.MultipleMids:
                    Console.WriteLine("multiple music matches: ");
                    needsPrint = true;
                    break;
                case GGVCInnerResultKind.NoMids:
                    Console.WriteLine("no music matches: ");
                    needsPrint = true;
                    break;
                case GGVCInnerResultKind.NoAids:
                    Console.WriteLine("no artist id matches: ");
                    needsPrint = true;
                    break;
                case GGVCInnerResultKind.NoSources:
                    Console.WriteLine("no source matches: ");
                    needsPrint = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (needsPrint)
            {
                Console.WriteLine(egsImporterInnerResult.GGVCSong.Path);
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.GGVCSong.Sources, Utils.Jso));
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.GGVCSong.Titles, Utils.Jso));
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.GGVCSong.Artists, Utils.Jso));
                Console.WriteLine($"aIds{JsonSerializer.Serialize(egsImporterInnerResult.aIds, Utils.Jso)}");
                Console.WriteLine($"mIds{JsonSerializer.Serialize(egsImporterInnerResult.mIds, Utils.Jso)}");
                Console.WriteLine("--------------");
            }
        }

        Console.WriteLine("total count: " + ggvcSongs.Count);
        Console.WriteLine("matchedMids count: " +
                          ggvcInnerResults.Count(x => x.ResultKind == GGVCInnerResultKind.Matched));
        Console.WriteLine("multipleMids count: " +
                          ggvcInnerResults.Count(x => x.ResultKind == GGVCInnerResultKind.MultipleMids));
        Console.WriteLine("noMids count: " +
                          ggvcInnerResults.Count(x => x.ResultKind == GGVCInnerResultKind.NoMids));
        Console.WriteLine("noAids count: " +
                          ggvcInnerResults.Count(x => x.ResultKind == GGVCInnerResultKind.NoAids));
        Console.WriteLine("noSources count: " +
                          ggvcInnerResults.Count(x => x.ResultKind == GGVCInnerResultKind.NoSources));

        // todo
        ggvcInnerResults =
            new ConcurrentBag<GGVCInnerResult>(ggvcInnerResults.Where(x =>
                !x.GGVCSong.Path.Contains("Galgame Vocal Collection [EX]")));

        const string folder = "C:\\emq\\ggvc3";
        await File.WriteAllTextAsync($"{folder}\\noSources.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.NoSources),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{folder}\\noMids.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.NoMids),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{folder}\\noAids.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.NoAids),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{folder}\\matched.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.Matched),
                Utils.JsoIndented));
    }
}

public readonly struct GGVCSong
{
    public string Path { get; init; }

    public List<string> Sources { get; init; }

    public List<string> Titles { get; init; }

    public List<string> Artists { get; init; }
}

public class GGVCInnerResult
{
    public GGVCSong GGVCSong { get; set; }

    public List<int> aIds { get; set; } = new();

    public List<int> mIds { get; set; } = new();

    public GGVCInnerResultKind ResultKind { get; set; }
}

public enum GGVCInnerResultKind
{
    NoSources,
    NoAids,
    MultipleMids,
    NoMids,
    Matched,
}
