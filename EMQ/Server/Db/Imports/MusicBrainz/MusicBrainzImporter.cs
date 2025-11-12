using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.MusicBrainz.Model;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
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

        if (!ConnectionHelper.GetConnectionString().Contains("DATABASE=EMQ;", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Database name in the connstr must be 'EMQ'");
        }

        PendingSongs.Clear();
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        Console.WriteLine(
            $"StartSection start: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        string date = Constants.ImportDateMusicBrainz; // todo param
        string folder;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            folder = $"C:/emq/musicbrainz/{date}";
        }
        else
        {
            folder = $"musicbrainzimporter/{date}";
            Console.WriteLine($"{Directory.GetCurrentDirectory()}/{folder}");
        }

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

        await File.WriteAllTextAsync($"{folder}/MusicBrainzImporter.json",
            System.Text.Json.JsonSerializer.Serialize(incomingSongs, Utils.Jso));

        Console.WriteLine(
            $"StartSection InsertMissingMusicSources: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        incomingSongs = await InsertMissingMusicSources(incomingSongs, calledFromApi);

        // todo figure out how to do this on the server
        // todo merge songs
        bool mergeRecordings = false;
        if (mergeRecordings)
        {
            Console.WriteLine(
                $"StartSection UpdateMergedRecordings: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

            const string sqlRedirect = "SELECT new_id FROM recording_gid_redirect WHERE gid = @gid";
            const string sqlRecordingGid = "SELECT gid FROM recording WHERE id = @id";

            async Task<Guid> GetMergedRecordingGid(NpgsqlTransaction transaction, Guid gid)
            {
                var connection = transaction.Connection!;
                int newId = await connection.QuerySingleOrDefaultAsync<int>(sqlRedirect, new { gid });
                if (newId > 0)
                {
                    gid = await connection.QuerySingleAsync<Guid>(sqlRecordingGid, new { id = newId });
                    await GetMergedRecordingGid(transaction, gid);
                }

                return gid;
            }

            IEnumerable<Guid> oldGids = incomingSongs.Select(x => x.MusicBrainzRecordingGid!.Value);
            await Parallel.ForEachAsync(oldGids,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 0 },
                async (oldGid, ct) =>
                {
                    await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Mb());
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    Guid newGid = await GetMergedRecordingGid(transaction, oldGid);
                    if (oldGid != newGid)
                    {
                        await using var connectionEmq =
                            new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                        Console.WriteLine($"updating merged recording {oldGid} => {newGid}");

                        // todo mel
                        bool success = true;
                        int rowsMusic = await connectionEmq.ExecuteAsync(
                            "UPDATE music SET musicbrainz_recording_gid = @newGid WHERE musicbrainz_recording_gid = @oldGid",
                            new { oldGid, newGid }, transaction);

                        int rowsRr = await connectionEmq.ExecuteAsync(
                            "UPDATE musicbrainz_release_recording SET recording = @newGid WHERE recording = @oldGid",
                            new { oldGid, newGid }, transaction);

                        // todo handle track_gid_redirect as well
                        // todo music_external_link
                        int rowsTr = await connectionEmq.ExecuteAsync(
                            "UPDATE musicbrainz_track_recording SET recording = @newGid WHERE recording = @oldGid",
                            new { oldGid, newGid }, transaction);

                        success &= rowsMusic > 0;
                        success &= rowsRr > 0;
                        success &= rowsTr > 0;

                        if (success)
                        {
                            await transaction.CommitAsync(ct);
                            foreach (Song song in incomingSongs.Where(x => x.MusicBrainzRecordingGid == oldGid))
                            {
                                song.Links.Single(x => x.Type == SongLinkType.MusicBrainzRecording).Url =
                                    $"https://musicbrainz.org/recording/{newGid}";
                            }
                        }
                        else
                        {
                            Console.WriteLine($"failed to update merged recording {oldGid} => {newGid}");
                        }
                    }
                });
        }

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
                    // Console.WriteLine("ignoring InsertMusicBrainzReleaseRecording error");
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
                    // Console.WriteLine("ignoring InsertMusicBrainzReleaseVgmdbAlbum error");
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
                    // Console.WriteLine("ignoring InsertMusicBrainzTrackRecording error");
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

        bool insertDirectly = false; // todo remove
        List<Song> canInsertDirectly = new();
        List<Song> canNotInsertDirectly = new();
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
                        int _ = await DbManager.InsertSong(song, isImport: true);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(song, Utils.JsoIndented));
                        throw;
                    }
                }
                else
                {
                    // todo remove
                    string[] ignored =
                    {
                        "5a2bb3bc-dc3d-4caf-9248-86cf7dc9d1bf", "c9e90d45-9061-491f-98bf-9fec26745b77",
                        "f7b9d9c7-7bc9-4b9c-a055-60d296764840", "4d2d81b0-66c4-4ade-a4d3-c259b250032d",
                        "82599d40-e312-49b8-9ed3-fa220f17e8d5", "586b72ed-3849-40c8-b58c-c80f2362ba75",
                        "cb372a45-3416-4a14-8500-85f26f08db90", "3fed0826-1cc5-401e-9bc3-876ad44d329f",
                        "a2e2d706-07aa-478b-9628-2479a4b3f778", "681e6a1b-e6ed-4b6e-adbe-daa035743b67",
                        "111ee5a0-3733-428e-8162-2990744252f8", "399f12e4-fe09-4af7-942e-225b3d92a2cc",
                        "f8369b44-a022-43c8-bd28-c8b646f52b01", "172abbd0-741a-4de9-9f93-e90daa0d99cb",
                        "29f60874-3fd6-46b3-94fd-4596cb21da6d", "6fb243fd-04e4-452f-be6b-512104099c34",
                        "7ec4ede3-53e0-459d-ac2c-bcc006714d5c", "c82dd4f7-fbae-4782-b4a6-2946b85c69a7",
                    };

                    if (insertDirectly && ignored.Contains(song.MusicBrainzRecordingGid.ToString()))
                    {
                        continue;
                    }

                    // todo? batch
                    await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                    string[] releaseUrls = song.Sources.SelectMany(x =>
                            x.Links.Where(y => y.Type == SongSourceLinkType.MusicBrainzRelease).Select(x => x.Url))
                        .ToArray();
                    bool releaseInEmq = await connection.ExecuteScalarAsync<bool>(
                        "SELECT 1 FROM music_source_external_link where url = ANY(@releaseUrls)",
                        new { releaseUrls });
                    if (insertDirectly || !releaseInEmq)
                    {
                        canInsertDirectly.Add(song);
                        continue;
                    }

                    canNotInsertDirectly.Add(song);
                }
            }
            else
            {
                // Console.WriteLine($"skipping existing song: {song}");
            }
        }

        PendingSongs.AddRange(canNotInsertDirectly);
        foreach (Song song in canInsertDirectly)
        {
            string recordingUrl = song.Links.First(x => x.Type == SongLinkType.MusicBrainzRecording).Url;
            Console.WriteLine($"inserting non-existing-source song: {song}");
            var actionResult = await ServerUtils.BotEditSong(new ReqEditSong(song, true, "BGM"));
            if (actionResult is not OkResult)
            {
                var badRequestObjectResult = actionResult as BadRequestObjectResult;
                Console.WriteLine(
                    $"actionResult is not OkResult: {song} {recordingUrl} {badRequestObjectResult?.Value}");
                // throw new Exception($"failed to BotEditSong {recordingUrl}");
            }
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"StartSection end: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        await DbManager.Init();
        await DbManager.RefreshAutocompleteFiles();
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static async Task<ResImportMusicBrainzDataInner> ImportMusicBrainzDataInner(
        IEnumerable<MusicBrainzJson> json)
    {
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

                // characters linked to their cv. instead of their own pages break stuff
                // e.g.: https://musicbrainz.org/recording/6b877bfb-916e-4b8c-80ca-926c4b11eda0
                data.artist = data.artist.DistinctBy(x => x.id).ToArray();

                // todo cv.
                var songArtists = new List<SongArtist>();
                foreach (artist artist in data.artist)
                {
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
                        // Console.WriteLine(
                        //     $"artist not linked: https://musicbrainz.org/artist/{artist.gid} {artist.name} {artist.sort_name}");
                    }

                    songArtistLinks.Add(new SongArtistLink
                    {
                        Url = $"https://musicbrainz.org/artist/{artist.gid}",
                        Type = SongArtistLinkType.MusicBrainzArtist,
                        Name = "",
                    });

                    // if (vndbid == "s39" && data.release.gid.ToString() == "4580ceeb-b77b-4870-aae9-ddad74805459")
                    // {
                    // }

                    if (songArtists.Any(x =>
                            x.Links.Any(y => y.Type == SongArtistLinkType.VNDBStaff && y.Url.ToVndbId() == vndbid) ||
                            x.Links.Any(y =>
                                y.Type == SongArtistLinkType.MusicBrainzArtist &&
                                y.Url.Replace("https://musicbrainz.org/artist/", "") == vndbid)))
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
                    };
                    songArtists.Add(songArtist);
                }

                var musicSourceTitles = new List<Title>()
                {
                    new Title { LatinTitle = data.release.name, NonLatinTitle = data.release.name, }
                };

                var song = new Song()
                {
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
                                    Name = "", // todo?
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
                    Links = new List<SongLink>
                    {
                        new()
                        {
                            Type = SongLinkType.MusicBrainzRecording,
                            Url = $"https://musicbrainz.org/recording/{data.recording.gid}",
                        }
                    }
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

        string date = Constants.ImportDateVndb;
        string folder;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            folder = $"C:/emq/vndb/{date}";
        }
        else
        {
            folder = $"vndbimporter/{date}";
            Console.WriteLine($"{Directory.GetCurrentDirectory()}/{folder}");
        }

        List<dynamic> musicSourcesJson = new();
        List<dynamic> musicSourcesTitlesJson = new();
        List<VNTagInfo> vnTagInfoJson = new();
        var tagsJson = JsonConvert.DeserializeObject<List<Tag>>(
            await File.ReadAllTextAsync($"{folder}/EMQ tags.json"))!;

        var vid = missingVndbUrls.Select(x => x.ToVndbId()).ToList();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()
                         .Replace("DATABASE=EMQ;", $"DATABASE={vndbDbName};", StringComparison.OrdinalIgnoreCase)))
        {
            Directory.SetCurrentDirectory(executingDirectory);
            string mbQueriesDir = @"../../../../Queries/MusicBrainz";
            if (calledFromApi)
            {
                mbQueriesDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? @"../../Queries/MusicBrainz"
                    : @"../Queries/MusicBrainz";
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
                    if (!musicSourcesDict.TryGetValue(dynData.VNID, out dynamic? dynMusicSource))
                    {
                        continue;
                    }

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
                            if (tagsDict.TryGetValue(tvi.t, out var tag))
                            {
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
                            else
                            {
                                Console.WriteLine($"tag not found: {tvi.t}");
                            }
                        }
                    }
                    else
                    {
                        // Console.WriteLine(
                        //     JsonConvert.SerializeObject(dynData.VNID +
                        //                                 $" has no tags: {JsonConvert.SerializeObject(vnTagInfo)}"));
                    }

                    int dateInt = (int)dynMusicSource.air_date_start;
                    if (dateInt.ToString().EndsWith("9999"))
                    {
                        dateInt -= 9898;
                    }
                    else if (dateInt.ToString().EndsWith("99"))
                    {
                        dateInt -= 98;
                    }

                    var airDateStart =
                        DateTime.ParseExact(dateInt.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);

                    var musicSourceTitles = new List<Title>();
                    foreach (dynamic dynMusicSourceTitle in dynMusicSourceTitles)
                    {
                        if (!(bool)dynMusicSourceTitle.official)
                        {
                            continue;
                        }

                        (string latinTitle, string? nonLatinTitle) = Utils.VndbTitleToEmqTitle(
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
