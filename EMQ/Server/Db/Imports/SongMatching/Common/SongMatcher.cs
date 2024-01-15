using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using DapperQueryBuilder;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Npgsql;
using TagLib;
using File = System.IO.File;

namespace EMQ.Server.Db.Imports.SongMatching.Common;

public static class SongMatcher
{
    // todo remove cacheFilePaths
    public static List<SongMatch> ParseSongFile(string dir, Regex regex, List<string> extensions,
        bool cacheFilePaths = false, bool alwaysUseMetadata = true, bool skipNonVocalSongs = true)
    {
        var songMatches = new ConcurrentBag<SongMatch>();

        List<string> filePaths = new();
        if (cacheFilePaths && File.Exists($"{dir}\\filePaths.json"))
        {
            filePaths = JsonSerializer.Deserialize<List<string>>(
                File.ReadAllText($"{dir}\\filePaths.json"), Utils.JsoIndented)!;
        }
        else
        {
            foreach (string extension in extensions)
            {
                filePaths.AddRange(Directory.GetFiles(dir, $"*.{extension}", SearchOption.AllDirectories));
            }

            if (cacheFilePaths)
            {
                File.WriteAllText($"{dir}\\filePaths.json",
                    JsonSerializer.Serialize(filePaths, Utils.JsoIndented));
            }
        }

        Parallel.ForEach(filePaths, (filePath, _) =>
        {
            string fileName = Path.GetFileName(filePath);

            if (skipNonVocalSongs &&
                (fileName.ToLowerInvariant().Contains("mix") ||
                 fileName.ToLowerInvariant().Contains("ｍｉｘ") ||
                 fileName.ToLowerInvariant().Contains("rmx") ||
                 fileName.ToLowerInvariant().Contains("radioedit") ||
                 fileName.ToLowerInvariant().Contains("arrang") ||
                 fileName.ToLowerInvariant().Contains("アレンジ") ||
                 fileName.ToLowerInvariant().Contains("acoustic") ||
                 fileName.ToLowerInvariant().Contains("裏ver") ||
                 fileName.ToLowerInvariant().Contains("off vocal") ||
                 fileName.ToLowerInvariant().Contains("offvocal") ||
                 fileName.ToLowerInvariant().Contains("no vocal") ||
                 fileName.ToLowerInvariant().Contains("instrumental") ||
                 fileName.ToLowerInvariant().Contains("(インスト)") ||
                 fileName.ToLowerInvariant().Contains("version-") ||
                 (fileName.ToLowerInvariant().Contains("ver.") &&
                  !fileName.ToLowerInvariant().Contains($"forever")
                  && !fileName.ToLowerInvariant().Contains($"lover"))))
            {
                Console.WriteLine("skipping: " + fileName);
                return;
            }

            var match = regex.Match(fileName);
            // Console.WriteLine("match: " + match.ToString());

            if (string.IsNullOrWhiteSpace(match.ToString()) && !string.IsNullOrWhiteSpace(regex.ToString()))
            {
                Console.WriteLine("regex didn't match: " + filePath);
            }

            string source = match.Groups[1].Value;
            string title = match.Groups[2].Value;
            string artist = match.Groups[3].Value;

            List<string> sources = new() { source.Trim() };
            List<string> titles = new() { title.Trim() };
            List<string> artists = new() { artist.Trim() };

            string? musicbrainzRecording = null;

            if (alwaysUseMetadata || string.IsNullOrWhiteSpace(match.ToString()))
            {
                try
                {
                    var tFile = TagLib.File.Create(filePath, ReadStyle.PictureLazy);
                    string? metadataSources = tFile.Tag.Album;
                    string? metadataTitle = tFile.Tag.Title;
                    string[] metadataArtists = tFile.Tag.Performers.Concat(tFile.Tag.AlbumArtists).ToArray();

                    // this is actually the recording gid even though it says track id (???)
                    musicbrainzRecording = tFile.Tag.MusicBrainzTrackId;
                    if (string.IsNullOrEmpty(musicbrainzRecording))
                    {
                        // work-around for TagLib not being able to parse MusicBrainz tags in some MP3s
                        if (Guid.TryParse(Path.GetFileNameWithoutExtension(filePath), out var guid))
                        {
                            musicbrainzRecording = guid.ToString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(metadataSources))
                    {
                        sources.Add(metadataSources);
                    }

                    if (!string.IsNullOrWhiteSpace(metadataTitle))
                    {
                        titles.Add(metadataTitle);
                    }

                    artists.AddRange(metadataArtists);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"TagLib exception for {filePath}: " + e.Message);
                }
            }

            sources = sources.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            titles = titles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            artists = artists.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

            if (titles.Any() && artists.Any())
            {
                var songMatch = new SongMatch
                {
                    Path = filePath,
                    Sources = sources,
                    Titles = titles,
                    Artists = artists,
                    MusicBrainzRecording = musicbrainzRecording
                };
                songMatches.Add(songMatch);
            }
        });

        return songMatches.ToList();
    }

    public static async Task Match(List<SongMatch> songMatches, string outputDir, bool useSource = true)
    {
        // todo? param
        var songSourceSongTypes = new[] { SongSourceSongType.OP, SongSourceSongType.ED, SongSourceSongType.Insert };

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

                List<int>? msIds = null;
                if (useSource)
                {
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

                    msIds = (await queryMusicSource.QueryAsync<int>()).ToList();
                    if (!msIds.Any())
                    {
                        innerResult.ResultKind = SongMatchInnerResultKind.NoSources;
                        return;
                    }
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

                if (useSource)
                {
                    queryMusic.Where(
                        $"ms.id = ANY({msIds})");
                }

                queryMusic.Where(
                    $"(mt.latin_title % ANY({songMatch.Titles}) OR mt.non_latin_title % ANY({songMatch.Titles}))");
                queryMusic.Where(
                    $"a.id = ANY({aIds})");
                queryMusic.Where(
                    $"msm.type = ANY({songSourceSongTypes.Cast<int>().ToArray()})");

                // Console.WriteLine(queryMusic.Sql);
                //Console.WriteLine(JsonSerializer.Serialize(queryMusic.Parameters, Utils.JsoIndented));

                var mids = (await queryMusic.QueryAsync<int>()).ToList();
                if (mids.Any())
                {
                    innerResult.mIds.AddRange(mids);
                    if (mids.Count > 1)
                    {
                        if (mids.All(x => midsWithSoundLinks.Contains(x)))
                        {
                            innerResult.ResultKind = SongMatchInnerResultKind.AlreadyHave;
                            return;
                        }

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

        var matched = songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Matched).ToList();
        foreach (var egsImporterInnerResult in matched)
        {
            string currentPath = egsImporterInnerResult.SongMatch.Path;
            if (egsImporterInnerResult.mIds.Any(x =>
                    matched.Any(y => y.mIds.Contains(x) && y.SongMatch.Path != currentPath)))
            {
                egsImporterInnerResult.ResultKind = SongMatchInnerResultKind.Duplicate;
            }
        }

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
                case SongMatchInnerResultKind.Duplicate:
                    Console.WriteLine("Duplicate: ");
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
        Console.WriteLine("Duplicate count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.Duplicate));

        Directory.CreateDirectory(outputDir);

        await File.WriteAllTextAsync($"{outputDir}\\matched.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Matched),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\alreadyHave.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.AlreadyHave),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\duplicate.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Duplicate),
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

    public static async Task MatchMusicBrainzRelease(List<SongMatch> songMatches, string outputDir, string releaseGid,
        bool useSource = true)
    {
        Dictionary<Guid, List<Guid>> musicBrainzRecordingReleases;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var musicBrainzReleaseRecordings = await connection.GetAllAsync<MusicBrainzReleaseRecording>();
            musicBrainzRecordingReleases = musicBrainzReleaseRecordings.GroupBy(x => x.recording)
                .ToDictionary(y => y.Key, y => y.Select(z => z.release).ToList());
        }

        var midsWithSoundLinks = await DbManager.FindMidsWithSoundLinks();
        var songMatchInnerResults = new ConcurrentBag<SongMatchInnerResult>();
        await Parallel.ForEachAsync(songMatches,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 3 },
            async (songMatch, _) =>
            {
                await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
                {
                    Console.WriteLine(songMatch.Path);
                    var innerResult = new SongMatchInnerResult() { SongMatch = songMatch };
                    songMatchInnerResults.Add(innerResult);

                    string? recordingStr = songMatch.MusicBrainzRecording;
                    if (!string.IsNullOrEmpty(recordingStr))
                    {
                        Console.WriteLine(recordingStr);

                        // if (recordingStr == "7ea10531-ca87-4234-a8e8-8f15fd93b683")
                        // {
                        // }

                        if (musicBrainzRecordingReleases.TryGetValue(new Guid(recordingStr), out var o))
                        {
                            Console.WriteLine(JsonSerializer.Serialize(o, Utils.Jso));
                            var queryMusic = connection
                                .QueryBuilder($@"SELECT DISTINCT m.id
FROM music m
/**where**/");

                            queryMusic.Where($"m.musicbrainz_recording_gid::text = {recordingStr}");

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
                        else
                        {
                            Console.WriteLine("recording didn't match");
                        }
                    }
                    else
                    {
                        Console.WriteLine("empty recording");
                    }
                }
            });

        // we don't care about duplicates here because we have the safety guarantee of a recording match
        // todo don't do this if we ever stop checking recordings
        var matched = songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Matched).ToList();
        matched = matched.DistinctBy(x => x.mIds.SingleOrDefault()).ToList();

        // foreach (SongMatchInnerResult songMatchInnerResult in songMatchInnerResults)
        // {
        //     string currentPath = songMatchInnerResult.SongMatch.Path;
        //     if (songMatchInnerResult.mIds.Any(x =>
        //             songMatchInnerResults.Any(y => y.mIds.Contains(x) && y.SongMatch.Path != currentPath)))
        //     {
        //         if (!songMatchInnerResults.(out var o))
        //         {
        //             throw new Exception();
        //         }
        //     }
        // }

        // var matched = songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Matched).ToList();
        // foreach (var egsImporterInnerResult in matched)
        // {
        //     string currentPath = egsImporterInnerResult.SongMatch.Path;
        //     if (egsImporterInnerResult.mIds.Any(x =>
        //             matched.Any(y => y.mIds.Contains(x) && y.SongMatch.Path != currentPath)))
        //     {
        //         egsImporterInnerResult.ResultKind = SongMatchInnerResultKind.Duplicate;
        //     }
        // }

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
                case SongMatchInnerResultKind.Duplicate:
                    Console.WriteLine("Duplicate: ");
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
                          matched.Count);
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
        Console.WriteLine("Duplicate count: " +
                          songMatchInnerResults.Count(x => x.ResultKind == SongMatchInnerResultKind.Duplicate));

        Directory.CreateDirectory(outputDir);

        await File.WriteAllTextAsync($"{outputDir}\\matched.json",
            JsonSerializer.Serialize(
                matched,
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\alreadyHave.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.AlreadyHave),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{outputDir}\\duplicate.json",
            JsonSerializer.Serialize(
                songMatchInnerResults.Where(x => x.ResultKind == SongMatchInnerResultKind.Duplicate),
                Utils.JsoIndented));

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
