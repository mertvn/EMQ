﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.MusicBrainz.Model;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;
using Npgsql;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EMQ.Server.Db.Imports.MusicBrainz;

public class ResImportMusicBrainzDataInner
{
    public List<Song> Songs { get; set; } = new();

    public List<MusicBrainzReleaseRecording> MusicBrainzReleaseRecordings { get; set; } = new();

    public List<MusicBrainzReleaseVgmdbAlbum> MusicBrainzReleaseVgmdbAlbums { get; set; } = new();

    public List<MusicBrainzTrackRecording> MusicBrainzTrackRecordings { get; set; } = new();
}

public class MusicBrainzVndbArtistJson
{
    public Guid mb { get; set; } = Guid.Empty;

    public string[] vndbid { get; set; } = Array.Empty<string>();
}

public static class MusicBrainzImporter
{
    public static List<Song> PendingSongs { get; set; } = new();

    public static Dictionary<Guid, string[]> MusicBrainzVndbArtistDict { get; set; } = new();

    public static async Task ImportMusicBrainzData(bool isIncremental, bool calledFromApi)
    {
        if (!isIncremental && ConnectionHelper.GetConnectionString().Contains("erogemusicquiz.com"))
        {
            throw new Exception("wrong db");
        }

        if (ConnectionHelper.GetConnectionString().Contains("AUTH"))
        {
            throw new Exception("wrong db");
        }

        PendingSongs.Clear();
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        Console.WriteLine(
            $"StartSection start: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        string date = Constants.ImportDateMusicBrainz; // todo param
        string folder = $"C:\\emq\\musicbrainz\\{date}";
        string file = await File.ReadAllTextAsync($"{folder}/musicbrainz.json");
        var json = JsonSerializer.Deserialize<MusicBrainzJson[]>(file);

        string file1 = await File.ReadAllTextAsync($"{folder}/musicbrainz_vndb_artist.json");
        var json1 = JsonSerializer.Deserialize<MusicBrainzVndbArtistJson[]>(file1);
        MusicBrainzVndbArtistDict = json1!.ToDictionary(x => x.mb, x => x.vndbid);

        Console.WriteLine(
            $"StartSection ImportMusicBrainzDataInner: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        var resImportMusicBrainzDataInner = await ImportMusicBrainzDataInner(json!);
        var incomingSongs = resImportMusicBrainzDataInner.Songs;
        var musicBrainzReleaseRecordings = resImportMusicBrainzDataInner.MusicBrainzReleaseRecordings;
        var musicBrainzReleaseVgmdbAlbums = resImportMusicBrainzDataInner.MusicBrainzReleaseVgmdbAlbums;
        var musicBrainzTrackRecordings = resImportMusicBrainzDataInner.MusicBrainzTrackRecordings;

        if (incomingSongs.DistinctBy(x => x.MusicBrainzRecordingGid).Count() != incomingSongs.Count)
        {
            throw new Exception("duplicate recordings detected");
        }

        await File.WriteAllTextAsync("C:\\emq\\emqsongsmetadata\\MusicBrainzImporter.json",
            System.Text.Json.JsonSerializer.Serialize(incomingSongs, Utils.Jso));

        Console.WriteLine(
            $"StartSection InsertMissingMusicSources: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        incomingSongs = await InsertMissingMusicSources(incomingSongs, calledFromApi);

        Console.WriteLine(
            $"StartSection InsertMusicBrainzReleaseRecording: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        // return;
        foreach (var musicBrainzReleaseRecording in musicBrainzReleaseRecordings.DistinctBy(x =>
                     (x.release, x.recording)))
        {
            try
            {
                // todo check existence before trying to insert
                await DbManager.InsertEntity(musicBrainzReleaseRecording);
            }
            catch (Exception)
            {
                if (isIncremental)
                {
                    Console.WriteLine("ignoring InsertMusicBrainzReleaseRecording error");
                }
                else
                {
                    Console.WriteLine(JsonSerializer.Serialize(musicBrainzReleaseRecording, Utils.JsoIndented));
                    throw;
                }
            }
        }

        Console.WriteLine(
            $"StartSection InsertMusicBrainzReleaseVgmdbAlbum: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        // return;
        foreach (var musicBrainzReleaseVgmdbAlbum in musicBrainzReleaseVgmdbAlbums.DistinctBy(x =>
                     (x.release, x.album_id)))
        {
            try
            {
                // todo check existence before trying to insert
                await DbManager.InsertEntity(musicBrainzReleaseVgmdbAlbum);
            }
            catch (Exception)
            {
                if (isIncremental)
                {
                    Console.WriteLine("ignoring InsertMusicBrainzReleaseVgmdbAlbum error");
                }
                else
                {
                    Console.WriteLine(JsonSerializer.Serialize(musicBrainzReleaseVgmdbAlbum, Utils.JsoIndented));
                    throw;
                }
            }
        }

        Console.WriteLine(
            $"StartSection InsertMusicBrainzTrackRecording: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        // return;
        foreach (var musicBrainzTrackRecording in musicBrainzTrackRecordings.DistinctBy(x =>
                     (x.track, x.recording)))
        {
            try
            {
                // todo check existence before trying to insert
                await DbManager.InsertEntity(musicBrainzTrackRecording);
            }
            catch (Exception)
            {
                if (isIncremental)
                {
                    Console.WriteLine("ignoring InsertMusicBrainzTrackRecording error");
                }
                else
                {
                    Console.WriteLine(JsonSerializer.Serialize(musicBrainzTrackRecording, Utils.JsoIndented));
                    throw;
                }
            }
        }

        Console.WriteLine(
            $"StartSection InsertSong: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        // return;

        List<Song> dbSongs;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
            Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
                .GroupBy(x => x.Item1)
                .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

            List<int> validMids = mids
                .Where(x => x.Value.Any(y => SongSourceSongTypeMode.BGM.ToSongSourceSongTypes().Contains(y)))
                .Select(z => z.Key)
                .ToList();

            dbSongs = await DbManager.SelectSongsMIds(validMids.ToArray(), false);
        }

        HashSet<Guid> dbHashes = dbSongs
            .Select(x => x.ToSongLite_MB())
            .Select(y => y.Recording).ToHashSet();

        HashSet<Guid> incomingHashes = incomingSongs
            .Select(x => x.ToSongLite_MB())
            .Select(y => y.Recording).ToHashSet();

        foreach (Song song in dbSongs)
        {
            var songLite = song.ToSongLite_MB();
            var hash = songLite.Recording;

            bool isInIncoming = incomingHashes.Contains(hash);
            if (!isInIncoming)
            {
                Console.WriteLine($"dbSong was not found in incoming: {song}");
            }
        }

        foreach (Song song in incomingSongs)
        {
            var songLite = song.ToSongLite_MB();
            var hash = songLite.Recording;

            bool isInDb = dbHashes.Contains(hash);
            if (!isInDb)
            {
                Console.WriteLine($"new/modified song: {song}");
                if (!isIncremental)
                {
                    try
                    {
                        int _ = await DbManager.InsertSong(song);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(song, Utils.JsoIndented));
                        throw;
                    }
                }
                else
                {
                    PendingSongs.Add(song);
                }
            }
            else
            {
                // Console.WriteLine($"skipping existing song: {song}");
            }
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"StartSection end: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static async Task<ResImportMusicBrainzDataInner> ImportMusicBrainzDataInner(
        IEnumerable<MusicBrainzJson> json)
    {
        bool b = true;
        if (b)
        {
            throw new Exception("i think i made the artist changes correctly but still be careful");
        }

        var songs = new List<Song>();
        var musicBrainzReleaseRecordings = new List<MusicBrainzReleaseRecording>();
        var musicBrainzReleaseVgmdbAlbums = new List<MusicBrainzReleaseVgmdbAlbum>();
        var musicBrainzTrackRecordings = new List<MusicBrainzTrackRecording>();

        var releaseGroups = json.OrderBy(x => x.release_group.id).GroupBy(y => y.release_group.id);
        foreach (IGrouping<int, MusicBrainzJson> releaseGroup in releaseGroups)
        {
            // pick the first non-blacklisted release (by id) of the release group and only import songs from that release
            var nonBlacklistedReleases = releaseGroup.Where(x =>
                !Blacklists.MusicBrainzImporterReleaseBlacklist.Contains(x.release.gid.ToString())).ToList();

            if (!nonBlacklistedReleases.Any())
            {
                // Console.WriteLine($"all releases in the release group are blacklisted: {releaseGroup.Key}");
                continue;
            }

            var selectedRelease = nonBlacklistedReleases
                .GroupBy(x => x.release.id)
                .OrderBy(y => y.Key).First();

            var selectedReleaseValues = selectedRelease
                .Select(x => x)
                .OrderBy(y => y.medium.position)
                .ThenBy(y => y.track.position).ToList();

            for (int index = 0; index < selectedReleaseValues.Count; index++)
            {
                MusicBrainzJson data = selectedReleaseValues.ElementAt(index);
                // Console.WriteLine($"{index} {data.release.gid}");

                // if (data.release.gid.ToString() == "19e9e324-e700-4c96-9a6c-cf958fb061e2")
                // {
                // }
                // if (data.release.gid.ToString() == "ee128312-f683-4c95-a305-fce268f00e0a")
                // {
                // }
                // if (data.release.gid.ToString() == "4f2a033a-6f05-4804-9c95-fd1a345bd232")
                // {
                // }

                // todo
                // if (data.release.gid.ToString() == "e666942f-2fb4-4738-bb9e-8e4c195f3191")
                // {
                // }

                // todo recording-vn blacklist

                // characters linked to their cv. instead of their own pages break stuff
                // e.g.: https://musicbrainz.org/recording/6b877bfb-916e-4b8c-80ca-926c4b11eda0
                data.artist = data.artist.DistinctBy(x => x.id).ToArray();

                // todo cv.
                var songArtists = new List<SongArtist>();
                foreach (artist artist in data.artist)
                {
                    // todo add both if we have them
                    List<SongArtistLink> songArtistLinks = new();
                    string vndbid;
                    if (MusicBrainzVndbArtistDict.TryGetValue(artist.gid, out var o))
                    {
                        vndbid = o.First(); // todo?

                        if (o.Length > 1)
                        {
                            Console.WriteLine(
                                $"artist has more than one vndb page linked: https://musicbrainz.org/artist/{artist.gid} {artist.name} {artist.sort_name}");
                        }

                        songArtistLinks.Add(new SongArtistLink
                        {
                            Url = vndbid.ToVndbUrl(), Type = SongArtistLinkType.VNDBStaff, Name = "",
                        });
                    }
                    else
                    {
                        vndbid = artist.gid.ToString();
                        songArtistLinks.Add(new SongArtistLink
                        {
                            Url = $"https://musicbrainz.org/artist/{artist.gid}",
                            Type = SongArtistLinkType.MusicBrainzArtist,
                            Name = "",
                        });
                        // Console.WriteLine(
                        //     $"artist not linked: https://musicbrainz.org/artist/{artist.gid} {artist.name} {artist.sort_name}");
                    }

                    // if (vndbid == "s39" && data.release.gid.ToString() == "4580ceeb-b77b-4870-aae9-ddad74805459")
                    // {
                    // }

                    if (songArtists.Any(x => x.VndbId == vndbid))
                    {
                        // https://musicbrainz.org/recording/59dbbff3-382e-4c8a-b276-df922a50e9a5
                        // Console.WriteLine($"skipping duplicate artist {JsonSerializer.Serialize(artist, Utils.Jso)}");
                        continue;
                    }

                    const SongArtistRole role = SongArtistRole.Unknown; // todo?
                    SongArtist songArtist = new SongArtist()
                    {
                        Links = songArtistLinks,
                        Roles = new List<SongArtistRole> { role },
                        PrimaryLanguage = artist.area.ToString(), // todo str
                        Titles =
                            new List<Title>()
                            {
                                new Title()
                                {
                                    LatinTitle = artist.sort_name,
                                    NonLatinTitle = artist.name,
                                    Language = artist.area.ToString() ?? "ja", // todo str
                                    IsMainTitle = false // todo?
                                },
                            },
                        Sex = (Sex)(artist.gender ?? 0) // todo str
                    };
                    songArtists.Add(songArtist);
                }

                var musicSourceTitles = new List<Title>()
                {
                    new Title { LatinTitle = data.release.name, NonLatinTitle = data.release.name, }
                };

                var song = new Song()
                {
                    MusicBrainzRecordingGid = data.recording.gid,
                    Type = SongType.Standard, // todo? detect karaoke songs
                    DataSource = DataSourceKind.MusicBrainz,
                    Titles =
                        new List<Title>()
                        {
                            new Title()
                            {
                                LatinTitle = data.track.name, // todo
                                NonLatinTitle = data.track.name, // todo
                                Language =
                                    "ja", // data.release.language.ToString()!, // TODO: release.language can be null
                                IsMainTitle = true
                            },
                        }.OrderBy(y => y.LatinTitle).ToList(),
                    Artists = songArtists.OrderBy(y => y.Titles.First().LatinTitle).ToList(),
                    Sources = new List<SongSource>()
                    {
                        new SongSource()
                        {
                            SongTypes = new List<SongSourceSongType>() { SongSourceSongType.BGM },
                            Links = new List<SongSourceLink>()
                            {
                                new SongSourceLink()
                                {
                                    Type = SongSourceLinkType.VNDB,
                                    Url = data.aaa_rids.vndbid.ToVndbUrl(),
                                    Name = "will be overridden", // todo?
                                },
                                new SongSourceLink()
                                {
                                    Type = SongSourceLinkType.MusicBrainzRelease,
                                    Url = $"https://musicbrainz.org/release/{data.release.gid}",
                                    Name = data.release.name,
                                },
                            },
                            Titles = musicSourceTitles,
                        },
                    },
                };

                if (!string.IsNullOrEmpty(data.aaa_rids.vgmdburl))
                {
                    song.Sources.Single().Links.Add(new SongSourceLink()
                    {
                        Type = SongSourceLinkType.VGMdbAlbum, Url = data.aaa_rids.vgmdburl, Name = "", // todo?
                    });

                    var musicBrainzReleaseVgmdbAlbum = new MusicBrainzReleaseVgmdbAlbum()
                    {
                        release = data.release.gid, album_id = int.Parse(data.aaa_rids.vgmdburl!.LastSegment())
                    };
                    musicBrainzReleaseVgmdbAlbums.Add(musicBrainzReleaseVgmdbAlbum);
                }

                Song? sameSong = songs.FirstOrDefault(x =>
                    x.MusicBrainzRecordingGid!.Value == song.MusicBrainzRecordingGid!.Value);

                if (sameSong is not null)
                {
                    var first = song.Sources.First().Titles.First().LatinTitle;
                    var second = sameSong.Sources.First().Titles.First().LatinTitle;
                    if (first != second)
                    {
                        Console.WriteLine($"Same song! {first} <-> {second}");
                    }

                    sameSong.Sources.AddRange(song.Sources.Except(sameSong.Sources));
                }
                else
                {
                    songs.Add(song);
                }

                var musicBrainzReleaseRecording = new MusicBrainzReleaseRecording
                {
                    release = data.release.gid, recording = data.recording.gid
                };
                musicBrainzReleaseRecordings.Add(musicBrainzReleaseRecording);

                var musicBrainzTrackRecording = new MusicBrainzTrackRecording
                {
                    track = data.track.gid, recording = data.recording.gid
                };
                musicBrainzTrackRecordings.Add(musicBrainzTrackRecording);
            }
        }

        var res = new ResImportMusicBrainzDataInner
        {
            Songs = songs,
            MusicBrainzReleaseRecordings = musicBrainzReleaseRecordings,
            MusicBrainzReleaseVgmdbAlbums = musicBrainzReleaseVgmdbAlbums,
            MusicBrainzTrackRecordings = musicBrainzTrackRecordings
        };
        return res;
    }

    private static async Task<List<Song>> InsertMissingMusicSources(List<Song> songs, bool calledFromApi)
    {
        var ret = songs;

        // find missing music sources
        List<string> vndbUrlsInDb;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            vndbUrlsInDb =
                (await connection.QueryAsync<string>(
                    $"select url from music_source_external_link where type = {(int)SongSourceLinkType.VNDB}"))
                .Distinct().ToList();
        }

        var incomingVndbUrls = songs.SelectMany(a => a.Sources)
            .SelectMany(x => x.Links.Where(y => y.Type == SongSourceLinkType.VNDB))
            .Select(z => z.Url)
            .Distinct()
            .ToList();

        var missingVndbUrls =
            incomingVndbUrls.Except(vndbUrlsInDb).OrderBy(x => int.Parse(x.ToVndbId().Replace("v", ""))).ToList();
        foreach (string missingVndbUrl in missingVndbUrls)
        {
            Console.WriteLine(missingVndbUrl);
        }

        // select data for missing music sources
        string vndbDbName = "vndbforemq";
        string executingDirectory = Directory.GetCurrentDirectory();
        string folder = $"C:\\emq\\vndb\\{Constants.ImportDateVndb}";

        List<dynamic> musicSourcesJson = new();
        List<dynamic> musicSourcesTitlesJson = new();
        List<VNTagInfo> vnTagInfoJson = new();
        var tagsJson = JsonConvert.DeserializeObject<List<Tag>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ tags.json"))!;

        var vid = missingVndbUrls.Select(x => x.ToVndbId()).ToList();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()
                         .Replace("DATABASE=EMQ;", $"DATABASE={vndbDbName};", StringComparison.OrdinalIgnoreCase)))
        {
            Directory.SetCurrentDirectory(executingDirectory);
            string mbQueriesDir = @"../../../../Queries/MusicBrainz";
            if (calledFromApi)
            {
                mbQueriesDir = @"../../Queries/MusicBrainz";
            }

            var queryNames = new List<string>()
            {
                "mb_EMQ music_source.sql", "mb_EMQ music_source_title.sql", "mb_EMQ vnTagInfo.sql",
            };

            foreach (string filePath in queryNames.Select(x => Path.Combine(mbQueriesDir, x)))
            {
                string filename = Path.GetFileNameWithoutExtension(filePath);
                string sql = await File.ReadAllTextAsync(filePath);

                var queryResult = (await connection.QueryAsync<dynamic>(sql, commandTimeout: 1000, param: new { vid }))
                    .ToList();
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

                switch (filename)
                {
                    case "mb_EMQ music_source":
                        musicSourcesJson = queryResult;
                        break;
                    case "mb_EMQ music_source_title":
                        musicSourcesTitlesJson = queryResult;
                        break;
                    case "mb_EMQ vnTagInfo":
                        vnTagInfoJson =
                            JsonConvert.DeserializeObject<List<VNTagInfo>>(JsonConvert.SerializeObject(queryResult))!;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        var musicSourcesDict = musicSourcesJson.ToDictionary(x => (string)x.id);
        var musicSourcesTitlesLookup = musicSourcesTitlesJson.ToLookup(x => (string)x.id);
        var tagsDict = tagsJson.ToDictionary(x => x.Id);

        // fill in data for missing music sources
        // todo: merge this stuff with VndbImporter.cs by extracting methods
        foreach (Song song in ret)
        {
            foreach (SongSource songSource in song.Sources)
            {
                var vndbLink = songSource.Links.Single(x => x.Type == SongSourceLinkType.VNDB);
                var songSourceVndbUrl = vndbLink.Url;
                if (missingVndbUrls.Contains(songSourceVndbUrl))
                {
                    var dynData = new ProcessedMusic { VNID = songSourceVndbUrl.ToVndbId() };

                    // Console.WriteLine($"Processing {JsonConvert.SerializeObject(dynData)}");
                    dynamic dynMusicSource = musicSourcesDict[dynData.VNID];
                    try
                    {
                        dynamic? _ = dynMusicSource.id;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"No matching music source found for {dynData.VNID}");
                        throw;
                    }

                    List<dynamic> dynMusicSourceTitles = musicSourcesTitlesLookup[dynData.VNID].ToList();
                    try
                    {
                        dynamic? _ = dynMusicSourceTitles.First().id;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"No matching music source title found for {dynData.VNID}");
                        throw;
                    }

                    List<SongSourceCategory> categories = new();

                    VNTagInfo? vnTagInfo = vnTagInfoJson.SingleOrDefault(x => x.VNID == dynData.VNID)!;
                    if (vnTagInfo != null! && vnTagInfo.TVIs.Any())
                    {
                        foreach (var tvi in vnTagInfo.TVIs)
                        {
                            var tag = tagsDict[tvi.t];
                            // Console.WriteLine(JsonConvert.SerializeObject(tag));

                            categories.Add(new SongSourceCategory
                            {
                                Name = tag.Name,
                                VndbId = tag.Id,
                                Type = SongSourceCategoryType.Tag,
                                Rating = tvi.r,
                                SpoilerLevel = (SpoilerLevel)tvi.s
                            });
                        }
                    }
                    else
                    {
                        // Console.WriteLine(
                        //     JsonConvert.SerializeObject(dynData.VNID +
                        //                                 $" has no tags: {JsonConvert.SerializeObject(vnTagInfo)}"));
                    }

                    int date = (int)dynMusicSource.air_date_start;
                    if (date.ToString().EndsWith("9999"))
                    {
                        date -= 9898;
                    }
                    else if (date.ToString().EndsWith("99"))
                    {
                        date -= 98;
                    }

                    var airDateStart =
                        DateTime.ParseExact(date.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);

                    var musicSourceTitles = new List<Title>();
                    foreach (dynamic dynMusicSourceTitle in dynMusicSourceTitles)
                    {
                        if (!(bool)dynMusicSourceTitle.official)
                        {
                            continue;
                        }

                        (string latinTitle, string? nonLatinTitle) = VndbImporter.VndbTitleToEmqTitle(
                            (string)dynMusicSourceTitle.title,
                            (string?)dynMusicSourceTitle.latin);

                        var musicSourceTitle = new Title()
                        {
                            LatinTitle = latinTitle,
                            NonLatinTitle = nonLatinTitle,
                            Language = dynMusicSourceTitle.lang,
                            IsMainTitle = string.Equals((string)dynMusicSourceTitle.lang,
                                (string)dynMusicSource.olang,
                                StringComparison.OrdinalIgnoreCase)
                        };
                        musicSourceTitles.Add(musicSourceTitle);
                    }

                    if (!musicSourceTitles.Any(x => x.IsMainTitle))
                    {
                        throw new Exception();
                    }

                    vndbLink.Name = musicSourceTitles.First(x => x.IsMainTitle).ToString();
                    songSource.AirDateStart = airDateStart;
                    songSource.SongTypes = new List<SongSourceSongType>() { SongSourceSongType.BGM };
                    songSource.LanguageOriginal = dynMusicSource.olang;
                    songSource.RatingAverage = dynMusicSource.c_average;
                    songSource.RatingBayesian = dynMusicSource.c_rating;
                    songSource.VoteCount = dynMusicSource.c_votecount;
                    songSource.Type = SongSourceType.VN;
                    songSource.Titles = musicSourceTitles;
                    songSource.Categories = categories;
                }
            }
        }

        return ret;
    }
}
