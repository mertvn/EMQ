using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using DapperQueryBuilder;
using EMQ.Shared.Core;
using Npgsql;

namespace EMQ.Server.Db.Imports.SongMatching;

public static class SongMatcher
{
    public static async Task Match(List<SongMatch> songMatches, string outputDir)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        }

        var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();
        var songMatchInnerResults = new ConcurrentBag<SongMatchInnerResult>();
        await Parallel.ForEachAsync(songMatches, async (songMatch, _) =>
        {
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                var innerResult = new SongMatchInnerResult() { SongMatch = songMatch };
                songMatchInnerResults.Add(innerResult);

                // Console.WriteLine(JsonSerializer.Serialize(songMatch.Artists, Utils.Jso));
                var aIds = await DbManager.FindArtistIdsByArtistNames(songMatch.Artists);
                innerResult.aIds = aIds;

                if (!aIds.Any())
                {
                    innerResult.ResultKind = SongMatchInnerResultKind.NoAids;
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
                    $"(mst.latin_title % ANY({songMatch.Sources}) OR mst.non_latin_title % ANY({songMatch.Sources}))");

                var msIds = (await queryMusicSource.QueryAsync<int>()).ToList();
                if (!msIds.Any())
                {
                    innerResult.ResultKind = SongMatchInnerResultKind.NoSources;
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
                    $"(mt.latin_title % ANY({songMatch.Titles}) OR mt.non_latin_title % ANY({songMatch.Titles}))");
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
                        innerResult.ResultKind = SongMatchInnerResultKind.MultipleMids;
                        return;
                    }

                    if (midsWithSoundLinks.Contains(mids.Single()))
                    {
                        innerResult.ResultKind = SongMatchInnerResultKind.AlreadyHave;
                        return;
                    }
                    else
                    {
                        innerResult.ResultKind = SongMatchInnerResultKind.Matched;
                        return;
                    }
                }
                else
                {
                    innerResult.ResultKind = SongMatchInnerResultKind.NoMids;
                    return;
                }
            }
        });

        foreach (var egsImporterInnerResult in songMatchInnerResults)
        {
            bool needsPrint;
            switch (egsImporterInnerResult.ResultKind)
            {
                case SongMatchInnerResultKind.Matched:
                    Console.WriteLine("matched: ");
                    needsPrint = true;
                    break;
                case SongMatchInnerResultKind.MultipleMids:
                    Console.WriteLine("multiple music matches: ");
                    needsPrint = true;
                    break;
                case SongMatchInnerResultKind.NoMids:
                    Console.WriteLine("no music matches: ");
                    needsPrint = true;
                    break;
                case SongMatchInnerResultKind.NoAids:
                    Console.WriteLine("no artist id matches: ");
                    needsPrint = true;
                    break;
                case SongMatchInnerResultKind.NoSources:
                    Console.WriteLine("no source matches: ");
                    needsPrint = true;
                    break;
                case SongMatchInnerResultKind.AlreadyHave:
                    Console.WriteLine("AlreadyHave: ");
                    needsPrint = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (needsPrint)
            {
                Console.WriteLine(egsImporterInnerResult.SongMatch.Path);
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.SongMatch.Sources, Utils.Jso));
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.SongMatch.Titles, Utils.Jso));
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.SongMatch.Artists, Utils.Jso));
                Console.WriteLine($"aIds{JsonSerializer.Serialize(egsImporterInnerResult.aIds, Utils.Jso)}");
                Console.WriteLine($"mIds{JsonSerializer.Serialize(egsImporterInnerResult.mIds, Utils.Jso)}");
                Console.WriteLine("--------------");
            }
        }

        Console.WriteLine("total count: " + songMatches.Count);
        Console.WriteLine("matchedMids count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.Matched));
        Console.WriteLine("multipleMids count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.MultipleMids));
        Console.WriteLine("noMids count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.NoMids));
        Console.WriteLine("noAids count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.NoAids));
        Console.WriteLine("noSources count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.NoSources));
        Console.WriteLine("AlreadyHave count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.AlreadyHave));

        Directory.CreateDirectory(outputDir);

        await File.WriteAllTextAsync($"{outputDir}\\matched.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Matched),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\alreadyHave.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.AlreadyHave),
                Utils.JsoIndented));

        // // todo
        // songMatchInnerResults =
        //     new ConcurrentBag<SongMatchInnerResult>(songMatchInnerResults.Where(x =>
        //         !x.SongMatch.Path.Contains("Galgame Vocal Collection [EX]")));

        await File.WriteAllTextAsync($"{outputDir}\\noSources.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.NoSources),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\noMids.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.NoMids),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\noAids.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.NoAids),
                Utils.JsoIndented));
    }
}
