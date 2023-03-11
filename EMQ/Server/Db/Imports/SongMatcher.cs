using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using DapperQueryBuilder;
using EMQ.Server.Db.Imports.GGVC;
using EMQ.Shared.Core;
using Npgsql;

namespace EMQ.Server.Db.Imports;

public static class SongMatcher
{
    public static async Task Match(List<GGVCSong> ggvcSongs, string outputDir)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        }

        var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();
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

                    if (midsWithSoundLinks.Contains(mids.Single()))
                    {
                        innerResult.ResultKind = GGVCInnerResultKind.AlreadyHave;
                        return;
                    }
                    else
                    {
                        innerResult.ResultKind = GGVCInnerResultKind.Matched;
                        return;
                    }
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
                case GGVCInnerResultKind.AlreadyHave:
                    Console.WriteLine("AlreadyHave: ");
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
        Console.WriteLine("AlreadyHave count: " +
                          ggvcInnerResults.Count(x => x.ResultKind == GGVCInnerResultKind.AlreadyHave));

        Directory.CreateDirectory(outputDir);

        await File.WriteAllTextAsync($"{outputDir}\\matched.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.Matched),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\alreadyHave.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.AlreadyHave),
                Utils.JsoIndented));

        // // todo
        // ggvcInnerResults =
        //     new ConcurrentBag<GGVCInnerResult>(ggvcInnerResults.Where(x =>
        //         !x.GGVCSong.Path.Contains("Galgame Vocal Collection [EX]")));

        await File.WriteAllTextAsync($"{outputDir}\\noSources.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.NoSources),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\noMids.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.NoMids),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\noAids.json",
            JsonSerializer.Serialize(
                ggvcInnerResults.Where(x => x.ResultKind == GGVCInnerResultKind.NoAids),
                Utils.JsoIndented));
    }
}
