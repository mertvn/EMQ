using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Dapper;
using Dapper.Database;
using Dapper.Database.Extensions;
using DapperQueryBuilder;
using EMQ.Client;
using EMQ.Client.Pages;
using EMQ.Server.Business;
using EMQ.Server.Controllers;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Entities.Auth;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz;
using EMQ.Shared.Quiz.Entities.Abstract;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EMQ.Server.Db;

public static class DbManager
{
    public static async Task Init()
    {
        SqlMapper.AddTypeHandler(typeof(MediaAnalyserResult), new JsonTypeHandler());
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

        var musicBrainzReleaseRecordings = await connection.GetListAsync<MusicBrainzReleaseRecording>();
        MusicBrainzRecordingReleases = musicBrainzReleaseRecordings.GroupBy(x => x.recording)
            .ToFrozenDictionary(y => y.Key, y => y.Select(z => z.release).ToList());

        var musicBrainzReleaseVgmdbAlbums = await connection.GetListAsync<MusicBrainzReleaseVgmdbAlbum>();
        MusicBrainzReleaseVgmdbAlbums = musicBrainzReleaseVgmdbAlbums.GroupBy(x => x.release)
            .ToFrozenDictionary(y => y.Key, y => y.Select(z => z.album_id).ToList());

        var musicBrainzTrackRecordings = await connection.GetListAsync<MusicBrainzTrackRecording>();
        MusicBrainzRecordingTracks = musicBrainzTrackRecordings.GroupBy(x => x.recording)
            .ToFrozenDictionary(y => y.Key, y => y.Select(z => z.track).ToList());

        IgnoredMusicVotes = (await connectionAuth.QueryAsync<int>("select id from users where ign_mv")).ToArray();

        string[] vnIds = (await connection.QueryAsync<string>(
                $"select url from music_source_external_link where type = {(int)SongSourceLinkType.VNDB}"))
            .Select(x => x.ToVndbId()).ToArray();

        // todo automate setting this to false when running freshsetup
        bool hasVndbDb = true;
        if (hasVndbDb)
        {
            await using (var connectionVndb = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb()))
            {
                const string sql = @"
SELECT distinct v.id, p.id, p.name, p.latin FROM producers p
join releases_producers rp ON rp.pid = p.id
JOIN releases r ON r.id = rp.id
JOIN releases_vn rv ON rv.id = r.id
JOIN vn v ON v.id = rv.vid
WHERE rp.developer AND r.official AND v.id = ANY(@vnIds)";

                var vnDevelopers =
                    await connectionVndb.QueryAsync<(string vId, string pId, string name, string? latin)>(sql,
                        new { vnIds });
                VnDevelopers = vnDevelopers.GroupBy(x => x.vId)
                    .ToFrozenDictionary(y => y.Key, y => y.Select(z => z).ToArray());
            }
        }

        await RefreshMusicIdsRecordingGidsCache();

        // await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        // {
        //     const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
        //     Dictionary<int, List<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
        //         .GroupBy(x => x.Item1)
        //         .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToList());
        //
        //     MusicIdsSongSourceSongTypes = mids;
        // }
    }

    private static ConcurrentDictionary<int, Song> CachedSongs { get; } = new();

    public static FrozenDictionary<Guid, List<Guid>> MusicBrainzRecordingReleases { get; set; } =
        FrozenDictionary<Guid, List<Guid>>.Empty;

    private static FrozenDictionary<Guid, List<int>> MusicBrainzReleaseVgmdbAlbums { get; set; } =
        FrozenDictionary<Guid, List<int>>.Empty;

    public static FrozenDictionary<Guid, List<Guid>> MusicBrainzRecordingTracks { get; set; } =
        FrozenDictionary<Guid, List<Guid>>.Empty;

    private static FrozenDictionary<int, Guid?> MusicIdsRecordingGids { get; set; } =
        FrozenDictionary<int, Guid?>.Empty;

    // public static Dictionary<int, List<SongSourceSongType>> MusicIdsSongSourceSongTypes { get; set; }

    public static FrozenDictionary<string, (string vId, string pId, string name, string? latin)[]>
        VnDevelopers { get; set; } =
        FrozenDictionary<string, (string vId, string pId, string name, string? latin)[]>.Empty;

    private static ConcurrentDictionary<string, LibraryStats?> CachedLibraryStats { get; } = new();

    private static int[] IgnoredMusicVotes { get; set; } = Array.Empty<int>();

    private static async Task RefreshMusicIdsRecordingGidsCache()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var musicIdsRecordingGids =
                await connection.QueryAsync<(int, Guid?)>("select id, musicbrainz_recording_gid from music");
            MusicIdsRecordingGids = musicIdsRecordingGids.ToFrozenDictionary(x => x.Item1, x => x.Item2);
        }
    }

    public static async Task RefreshAutocompleteFiles()
    {
        string autocompleteFolder = ServerState.AutocompleteFolder;
        await File.WriteAllTextAsync($"{autocompleteFolder}/mst.json",
            await DbManager.SelectAutocompleteMst());
        await File.WriteAllTextAsync($"{autocompleteFolder}/c.json",
            await DbManager.SelectAutocompleteC());
        await File.WriteAllTextAsync($"{autocompleteFolder}/a.json",
            await DbManager.SelectAutocompleteA());
        await File.WriteAllTextAsync($"{autocompleteFolder}/mt.json",
            await DbManager.SelectAutocompleteMt());
        await File.WriteAllTextAsync($"{autocompleteFolder}/developer.json",
            await DbManager.SelectAutocompleteDeveloper());
    }

    public static async Task<List<Song>> SelectSongsMIds(int[] mIds, bool selectCategories,
        NpgsqlTransaction? transaction = null)
    {
        if (!mIds.Any())
        {
            return new List<Song>();
        }

        return await SelectSongsBatch(mIds.Select(x => new Song() { Id = x }).ToList(), selectCategories, transaction);
    }

    public static async Task<List<Song>> SelectSongsMIdsCached(int[] mIds)
    {
        var cached = new List<Song>(mIds.Length);
        foreach (int key in mIds)
        {
            if (CachedSongs.TryGetValue(key, out var value))
            {
                cached.Add(value);
            }
        }

        var cachedMids = cached.Select(x => x.Id);
        int[] uncachedMids = mIds.Except(cachedMids).ToArray();

        List<Song> uncached = new();
        if (uncachedMids.Any())
        {
            uncached = await SelectSongsBatch(uncachedMids.Select(x => new Song() { Id = x }).ToList(), false);
            foreach (Song song in uncached)
            {
                if (!song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.BGM))
                {
                    CachedSongs[song.Id] = song;
                }
            }
        }

        return uncached.Concat(cached).ToList();
    }

    // todo overload that takes int[] mIds to avoid Song object creation cost
    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Titles.LatinTitle <br/>
    /// Song.Links.Url <br/>
    /// </summary>
    public static async Task<List<Song>> SelectSongsBatch(List<Song> input, bool selectCategories,
        NpgsqlTransaction? transaction = null)
    {
        NpgsqlConnection? connection = transaction?.Connection;
        bool ownConnection = false;
        if (connection is null)
        {
            ownConnection = true;
            connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            await connection.OpenAsync();
            transaction = await connection.BeginTransactionAsync();
        }

        var mIds = input.Select(x => x.Id).Where(x => x > 0).ToArray();
        var latinTitles = input.SelectMany(y => y.Titles.Select(x => x.LatinTitle)).ToList();
        var links = input.SelectMany(y => y.Links.Select(x => x.Url)).ToList();

        // if (mIds.Any() && !latinTitles.Any() && !links.Any() && !selectCategories)
        // {
        //     // todo? batch cache or just get rid of cache?
        //     if (CachedSongs.TryGetValue(input.Id, out var s))
        //     {
        //         s = JsonSerializer.Deserialize<Song>(JsonSerializer.Serialize(s)); // need deep copy
        //         return new List<Song> { s! };
        //     }
        // }

        var queryMusic = connection
            .QueryBuilder($@"SELECT *
            FROM music m
            LEFT JOIN music_title mt ON mt.music_id = m.id
            LEFT JOIN music_external_link mel ON mel.music_id = m.id
            /**where**/
    ");

        if (mIds.Any())
        {
            queryMusic.Where($"m.id = ANY({mIds})");
        }

        if (latinTitles.Any())
        {
            queryMusic.Where($"mt.latin_title = ANY({latinTitles})");
        }

        if (links.Any())
        {
            queryMusic.Where($"mel.url = ANY({links})");
        }

        if (queryMusic.GetFilters() is null)
        {
            return new List<Song>();
            throw new Exception("At least one filter must be applied");
        }

        // Console.WriteLine(queryMusic.Sql);

        var songs = new Dictionary<int, Song>();
        await connection.QueryAsync(queryMusic.Sql,
            new[] { typeof(Music), typeof(MusicTitle), typeof(MusicExternalLink), }, (objects) =>
            {
                // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                var music = (Music)objects[0];
                var musicTitle = (MusicTitle)objects[1];
                var musicExternalLink = (MusicExternalLink?)objects[2];

                if (!songs.TryGetValue(music.id, out Song? existingSong))
                {
                    var song = new Song();
                    var songTitles = new List<Title>();
                    var songLinks = new List<SongLink>();

                    song.Id = music.id;
                    song.Type = (SongType)music.type;
                    song.Attributes = (SongAttributes)music.attributes;
                    song.DataSource = music.data_source;
                    song.MusicBrainzRecordingGid = music.musicbrainz_recording_gid;
                    song.Stats = new SongStats()
                    {
                        TimesCorrect = music.stat_correct,
                        TimesPlayed = music.stat_played,
                        CorrectPercentage = music.stat_correctpercentage,
                        TimesGuessed = music.stat_guessed,
                        TotalGuessMs = music.stat_totalguessms,
                        AverageGuessMs = music.stat_averageguessms,
                        UniqueUsers = music.stat_uniqueusers,
                    };

                    songTitles.Add(new Title()
                    {
                        LatinTitle = musicTitle.latin_title,
                        NonLatinTitle = musicTitle.non_latin_title,
                        Language = musicTitle.language,
                        IsMainTitle = musicTitle.is_main_title
                    });

                    if (musicExternalLink is not null)
                    {
                        songLinks.Add(new SongLink()
                        {
                            Url = musicExternalLink.url.ReplaceSelfhostLink(),
                            IsVideo = musicExternalLink.is_video,
                            Type = (SongLinkType)musicExternalLink.type,
                            Duration = musicExternalLink.duration,
                            SubmittedBy = musicExternalLink.submitted_by,
                            Sha256 = musicExternalLink.sha256,
                            AnalysisRaw = musicExternalLink.analysis_raw,
                        });
                    }

                    song.Titles = songTitles.DistinctBy(x => x.LatinTitle).ToList();
                    song.Links = songLinks.DistinctBy(x => x.Url).ToList();

                    songs.Add(song.Id, song);
                }
                else
                {
                    if (!existingSong.Titles.Any(x =>
                            x.LatinTitle == musicTitle.latin_title && x.Language == musicTitle.language))
                    {
                        existingSong.Titles.Add(new Title()
                        {
                            LatinTitle = musicTitle.latin_title,
                            NonLatinTitle = musicTitle.non_latin_title,
                            Language = musicTitle.language
                        });
                    }

                    if (musicExternalLink != null)
                    {
                        if (!existingSong.Links.Any(x => x.Url == musicExternalLink.url))
                        {
                            existingSong.Links.Add(new SongLink()
                            {
                                Url = musicExternalLink.url.ReplaceSelfhostLink(),
                                Type = (SongLinkType)musicExternalLink.type,
                                IsVideo = musicExternalLink.is_video,
                                Duration = musicExternalLink.duration,
                                SubmittedBy = musicExternalLink.submitted_by,
                                Sha256 = musicExternalLink.sha256,
                                AnalysisRaw = musicExternalLink.analysis_raw,
                            });
                        }
                    }
                }

                return 0;
            },
            splitOn:
            "music_id,music_id", param: queryMusic.Parameters, transaction: transaction);

        foreach ((int key, Song? song) in songs)
        {
            if (song.MusicBrainzRecordingGid is not null)
            {
                song.MusicBrainzReleases = MusicBrainzRecordingReleases[song.MusicBrainzRecordingGid.Value];
                if (MusicBrainzRecordingTracks.TryGetValue(song.MusicBrainzRecordingGid.Value, out var tracks))
                {
                    song.MusicBrainzTracks = tracks;
                }
            }

            foreach (Guid songMusicBrainzRelease in song.MusicBrainzReleases)
            {
                // not every musicbrainz release we have is connected to a vgmdb album
                if (MusicBrainzReleaseVgmdbAlbums.TryGetValue(songMusicBrainzRelease, out var vgmdb))
                {
                    song.VgmdbAlbums.AddRange(vgmdb);
                }
            }
        }

        var sourcesDict = await SelectSongSourceBatch(connection, songs.Values.ToList(), selectCategories, transaction);
        var artistsDict = await SelectArtistBatch(connection, songs.Values.ToList(), false, transaction);

        foreach ((int _, Song? song) in songs)
        {
            song.Sources = sourcesDict[song.Id].Values.ToList();
            song.Artists = artistsDict[song.Id].Values.OrderBy(x => x.Roles.Min()).ToList();

            // if (!song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.BGM))
            // {
            //     CachedSongs[input.Id] = song;
            // }
        }

        // TOSCALE
        var musicVotes =
            (await connection.QueryAsync<MusicVote>(
                "select * from music_vote where music_id = ANY(@mIds) and not user_id = any(@ign)",
                new { mIds = songs.Select(x => x.Value.Id).ToArray(), ign = IgnoredMusicVotes }, transaction))
            .GroupBy(x => x.music_id).ToArray();

        foreach (IGrouping<int, MusicVote> musicVote in musicVotes)
        {
            songs[musicVote.Key].VoteAverage = (float)Math.Round(musicVote.Average(x => x.vote!.Value), 2) / 10;
            songs[musicVote.Key].VoteCount = musicVote.Count();
        }

        if (ownConnection)
        {
            await transaction!.CommitAsync();
            await connection.DisposeAsync();
            await transaction.DisposeAsync();
        }

        // Console.WriteLine("songs: " + JsonSerializer.Serialize(songs, Utils.JsoIndented));
        return songs.Values.ToList();
    }

    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Sources.Id <br/>
    /// Song.Sources.Links <br/>
    /// Song.Sources.Titles.LatinTitle <br/>
    /// Song.Sources.Titles.NonLatinTitle <br/>
    /// Song.Sources.Categories.VndbId <br/>
    /// </summary>
    public static async Task<Dictionary<int, Dictionary<int, SongSource>>> SelectSongSourceBatch(
        IDbConnection connection,
        List<Song> songs,
        bool selectCategories,
        NpgsqlTransaction? transaction = null)
    {
        var mIdSongSources = new Dictionary<int, Dictionary<int, SongSource>>();
        QueryBuilder queryMusicSource = connection.QueryBuilder($@"SELECT *
            FROM music_source_music msm
            LEFT JOIN music_source ms ON ms.id = msm.music_source_id
            LEFT JOIN music_source_title mst ON mst.music_source_id = ms.id
            LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
            /**where**/
    ");

        int[] mIds = songs.Select(a => a.Id).Where(x => x > 0).ToArray();
        if (mIds.Any())
        {
            queryMusicSource.Where($"msm.music_id = ANY({mIds})");
        }

        int?[] sourceIds = songs.Select(a => a.Sources.FirstOrDefault()?.Id).Where(x => x is > 0).ToArray();
        if (sourceIds.Any())
        {
            queryMusicSource.Where($"ms.id = ANY({sourceIds})");
        }

        List<string> latinTitles = songs.SelectMany(a => a.Sources.SelectMany(x => x.Titles.Select(y => y.LatinTitle)))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (latinTitles.Any())
        {
            // todo? ILIKE instead of =
            queryMusicSource.Where($"mst.latin_title = ANY({latinTitles})");
        }

        List<string> nonLatinTitles = songs
            .SelectMany(a => a.Sources.SelectMany(x => x.Titles.Select(y => y.NonLatinTitle)))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList()!;
        if (nonLatinTitles.Any())
        {
            queryMusicSource.Where($"mst.non_latin_title = ANY({nonLatinTitles})");
        }

        List<string> links = songs.SelectMany(a => a.Sources.SelectMany(x => x.Links.Select(y => y.Url))).ToList();
        if (links.Any())
        {
            queryMusicSource.Where($"msel.url = ANY({links})");
        }

        // todo needs to take type into account as well / or just query with Id instead of VndbId
        List<string?> categories = songs.SelectMany(a => a.Sources.SelectMany(x => x.Categories.Select(y => y.VndbId)))
            .ToList();
        if (categories.Any())
        {
            if (!selectCategories)
            {
                throw new ArgumentException(
                    $"Parameter {nameof(selectCategories)} must be set to true in order to filter by categories.",
                    nameof(selectCategories));
            }

            int[] msIdsWithCategories = (await connection.QueryAsync<int>(@"select music_source_id from
            music_source_category msc
            LEFT JOIN category c ON c.id = msc.category_id
            WHERE c.vndb_id = ANY(@categories)", new { categories }, transaction)).ToArray();
            queryMusicSource.Where($"ms.id = ANY({msIdsWithCategories})");
        }

        if (queryMusicSource.GetFilters() is null)
        {
            return mIdSongSources;
            throw new Exception("At least one filter must be applied");
        }

        // Console.WriteLine(queryMusicSource.Sql);
        await connection.QueryAsync(queryMusicSource.Sql,
            new[]
            {
                typeof(MusicSourceMusic), typeof(MusicSource), typeof(MusicSourceTitle),
                typeof(MusicSourceExternalLink)
            }, (objects) =>
            {
                // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                var musicSourceMusic = (MusicSourceMusic)objects[0];
                var musicSource = (MusicSource)objects[1];
                var musicSourceTitle = (MusicSourceTitle)objects[2];
                var musicSourceExternalLink = (MusicSourceExternalLink?)objects[3];

                List<Guid> musicBrainzReleases = new();
                if (MusicIdsRecordingGids.TryGetValue(musicSourceMusic.music_id, out var recording))
                {
                    if (recording is not null && recording != Guid.Empty)
                    {
                        musicBrainzReleases = MusicBrainzRecordingReleases[recording.Value];
                    }
                }

                List<int> vgmdbAlbums = new();
                foreach (Guid songMusicBrainzRelease in musicBrainzReleases)
                {
                    // not every musicbrainz release we have is connected to a vgmdb album
                    if (MusicBrainzReleaseVgmdbAlbums.TryGetValue(songMusicBrainzRelease, out var vgmdb))
                    {
                        vgmdbAlbums.AddRange(vgmdb);
                    }
                }

                // _ = songs.TryGetValue(musicSourceMusic.music_id, out var input);

                if (!mIdSongSources.TryGetValue(musicSourceMusic.music_id, out var songSources))
                {
                    Dictionary<int, SongSource> dict = new();
                    mIdSongSources.Add(musicSourceMusic.music_id, dict);
                    songSources = dict;
                }

                if (!songSources.TryGetValue(musicSource.id, out var existingSongSource))
                {
                    songSources.Add(musicSource.id, new SongSource()
                    {
                        Id = musicSource.id,
                        Type = (SongSourceType)musicSource.type,
                        AirDateStart = musicSource.air_date_start,
                        AirDateEnd = musicSource.air_date_end,
                        LanguageOriginal = musicSource.language_original,
                        RatingAverage = musicSource.rating_average,
                        RatingBayesian = musicSource.rating_bayesian,
                        // Popularity = musicSource.popularity,
                        VoteCount = musicSource.votecount,
                        SongTypes = new List<SongSourceSongType> { (SongSourceSongType)musicSourceMusic.type },
                        Titles = new List<Title>
                        {
                            new Title()
                            {
                                LatinTitle = musicSourceTitle.latin_title,
                                NonLatinTitle = musicSourceTitle.non_latin_title,
                                Language = musicSourceTitle.language,
                                IsMainTitle = musicSourceTitle.is_main_title
                            }
                        },
                        Links = new List<SongSourceLink>() { },
                        Categories = new List<SongSourceCategory>() { },
                        MusicIds = new()
                        {
                            { musicSourceMusic.music_id, new() { (SongSourceSongType)musicSourceMusic.type } }
                        }
                    });

                    if (musicSourceExternalLink is not null)
                    {
                        switch ((SongSourceLinkType)musicSourceExternalLink.type)
                        {
                            case SongSourceLinkType.MusicBrainzRelease:
                                {
                                    if (musicBrainzReleases.Contains(
                                            Guid.Parse(musicSourceExternalLink.url.LastSegment())))
                                    {
                                        songSources[musicSource.id].Links.Add(new SongSourceLink()
                                        {
                                            Url = musicSourceExternalLink.url,
                                            Type = (SongSourceLinkType)musicSourceExternalLink.type,
                                            Name = musicSourceExternalLink.name,
                                        });
                                    }

                                    break;
                                }
                            case SongSourceLinkType.VGMdbAlbum:
                                {
                                    if (vgmdbAlbums.Contains(
                                            int.Parse(musicSourceExternalLink.url.LastSegment())))
                                    {
                                        songSources[musicSource.id].Links.Add(new SongSourceLink()
                                        {
                                            Url = musicSourceExternalLink.url,
                                            Type = (SongSourceLinkType)musicSourceExternalLink.type,
                                            Name = musicSourceExternalLink.name,
                                        });
                                    }

                                    break;
                                }
                            case SongSourceLinkType.Unknown:
                            case SongSourceLinkType.VNDB:
                            default:
                                {
                                    songSources[musicSource.id].Links.Add(new SongSourceLink()
                                    {
                                        Url = musicSourceExternalLink.url,
                                        Type = (SongSourceLinkType)musicSourceExternalLink.type,
                                        Name = musicSourceExternalLink.name,
                                    });
                                    break;
                                }
                        }
                    }
                }
                else
                {
                    if (!existingSongSource.Titles.Any(x =>
                            x.LatinTitle == musicSourceTitle.latin_title && x.Language == musicSourceTitle.language))
                    {
                        existingSongSource.Titles.Add(new Title()
                        {
                            LatinTitle = musicSourceTitle.latin_title,
                            NonLatinTitle = musicSourceTitle.non_latin_title,
                            Language = musicSourceTitle.language,
                            IsMainTitle = musicSourceTitle.is_main_title,
                        });
                    }

                    if (musicSourceExternalLink is not null)
                    {
                        if (!existingSongSource.Links.Any(x => x.Url == musicSourceExternalLink.url))
                        {
                            switch ((SongSourceLinkType)musicSourceExternalLink.type)
                            {
                                case SongSourceLinkType.MusicBrainzRelease:
                                    {
                                        if (musicBrainzReleases.Contains(
                                                Guid.Parse(musicSourceExternalLink.url.LastSegment())))
                                        {
                                            songSources[musicSource.id].Links.Add(new SongSourceLink()
                                            {
                                                Url = musicSourceExternalLink.url,
                                                Type = (SongSourceLinkType)musicSourceExternalLink.type,
                                                Name = musicSourceExternalLink.name,
                                            });
                                        }

                                        break;
                                    }
                                case SongSourceLinkType.VGMdbAlbum:
                                    {
                                        if (vgmdbAlbums.Contains(
                                                int.Parse(musicSourceExternalLink.url.LastSegment())))
                                        {
                                            songSources[musicSource.id].Links.Add(new SongSourceLink()
                                            {
                                                Url = musicSourceExternalLink.url,
                                                Type = (SongSourceLinkType)musicSourceExternalLink.type,
                                                Name = musicSourceExternalLink.name,
                                            });
                                        }

                                        break;
                                    }
                                case SongSourceLinkType.Unknown:
                                case SongSourceLinkType.VNDB:
                                default:
                                    {
                                        songSources[musicSource.id].Links.Add(new SongSourceLink()
                                        {
                                            Url = musicSourceExternalLink.url,
                                            Type = (SongSourceLinkType)musicSourceExternalLink.type,
                                            Name = musicSourceExternalLink.name,
                                        });
                                        break;
                                    }
                            }
                        }
                    }

                    var songSourceSongType = (SongSourceSongType)musicSourceMusic.type;
                    if (existingSongSource.MusicIds.TryGetValue(musicSourceMusic.music_id, out var songSourceSongTypes))
                    {
                        songSourceSongTypes.Add(songSourceSongType);
                    }
                    else
                    {
                        existingSongSource.MusicIds[musicSourceMusic.music_id] = new() { songSourceSongType };
                    }
                }

                return 0;
            },
            splitOn: "id,music_source_id,music_source_id",
            param: queryMusicSource.Parameters, transaction: transaction);

        if (selectCategories && mIdSongSources.Any())
        {
            const string sqlCategories = @"SELECT * FROM music_source_category msc
            LEFT JOIN category c ON c.id = msc.category_id
            WHERE msc.music_source_id = ANY(@msIds)";

            int[] msIds = mIdSongSources.SelectMany(x => x.Value.Select(y => y.Key)).ToArray();
            var res = (await connection.QueryAsync<MusicSourceCategory, Category, (int, SongSourceCategory)>(
                sqlCategories, (msc, c) =>
                {
                    var songSourceCategory = new SongSourceCategory
                    {
                        Id = c.id,
                        Name = c.name,
                        VndbId = c.vndb_id,
                        Type = (SongSourceCategoryType)c.type,
                        Rating = msc.rating,
                        SpoilerLevel = (SpoilerLevel?)msc.spoiler_level,
                    };
                    return (msc.music_source_id, songSourceCategory);
                }, new { msIds }, splitOn: "id", transaction: transaction)).ToArray();

            foreach ((int mId, Dictionary<int, SongSource>? value) in mIdSongSources)
            {
                foreach ((int msId, SongSource? _) in value)
                {
                    var msCategories = res.Where(x => x.Item1 == msId).Select(x => x.Item2).ToList();
                    mIdSongSources[mId][msId].Categories = msCategories;
                }
            }
        }

        foreach ((int mId, Dictionary<int, SongSource>? value) in mIdSongSources)
        {
            foreach ((int msId, SongSource? songSource) in value)
            {
                // fix SongTypes for mIds
                mIdSongSources[mId][msId].SongTypes = songSource.MusicIds[mId].ToList();

                // fill developers
                var songSourceDevelopers = new List<SongSourceDeveloper>();
                foreach (string vndbId in songSource.Links.Where(y => y.Type == SongSourceLinkType.VNDB)
                             .Select(z => z.Url.ToVndbId()))
                {
                    if (DbManager.VnDevelopers.TryGetValue(vndbId, out var developers))
                    {
                        foreach ((string? _, string? pId, string? name, string? latin) in developers)
                        {
                            (string? latinTitle, string? nonLatinTitle) = VndbImporter.VndbTitleToEmqTitle(name, latin);
                            songSourceDevelopers.Add(new SongSourceDeveloper
                            {
                                VndbId = pId,
                                Title = new Title
                                {
                                    LatinTitle = latinTitle, NonLatinTitle = nonLatinTitle, IsMainTitle = true
                                }
                            });
                        }
                    }
                }

                mIdSongSources[mId][msId].Developers = songSourceDevelopers;
            }
        }

        return mIdSongSources;
    }

    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Artists.Id <br/>
    /// Song.Artists.Titles.ArtistAliasId <br/>
    /// Song.Artists.Titles.LatinTitle <br/>
    /// Song.Artists.Titles.NonLatinTitle <br/>
    /// Song.Artists.Links.Url <br/>
    /// </summary>
    public static async Task<Dictionary<int, Dictionary<int, SongArtist>>> SelectArtistBatch(IDbConnection connection,
        List<Song> songs,
        bool needsRequery,
        NpgsqlTransaction? transaction = null)
    {
        var mIdSongArtists = new Dictionary<int, Dictionary<int, SongArtist>>();
        var queryArtist = connection
            .QueryBuilder($@"SELECT *
            FROM artist_music am
            LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
            LEFT JOIN artist a ON a.id = aa.artist_id
            LEFT JOIN artist_external_link ael ON ael.artist_id = aa.artist_id
            /**where**/
    ");

        int[] mIds = songs.Select(x => x.Id).Where(x => x > 0).ToArray();
        if (mIds.Any())
        {
            queryArtist.Where($"am.music_id = ANY({mIds})");
        }

        int?[] artistIds = songs.Select(a => a.Artists.FirstOrDefault()?.Id).Where(x => x is > 0).ToArray();
        if (artistIds.Any())
        {
            queryArtist.Where($"a.id = ANY({artistIds})");
        }

        int[] artistAliasIds = songs.SelectMany(a =>
                a.Artists.FirstOrDefault()?.Titles.Select(x => x.ArtistAliasId) ?? Array.Empty<int>())
            .Where(x => x > 0).ToArray();
        if (artistAliasIds.Any())
        {
            queryArtist.Where($"aa.id = ANY({artistAliasIds})");
        }

        List<string> latinTitles = songs.SelectMany(a => a.Artists.SelectMany(x => x.Titles.Select(y => y.LatinTitle)))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (latinTitles.Any())
        {
            queryArtist.Where($"lower(aa.latin_alias) = ANY(lower({latinTitles}::text)::text[])");
        }

        List<string?> nonLatinTitles = songs
            .SelectMany(a => a.Artists.SelectMany(x => x.Titles.Select(y => y.NonLatinTitle)))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (nonLatinTitles.Any())
        {
            queryArtist.Where($"aa.non_latin_alias = ANY({nonLatinTitles})");
        }

        List<string> links = songs.SelectMany(a => a.Artists.SelectMany(x => x.Links.Select(y => y.Url))).ToList();
        if (links.Any())
        {
            queryArtist.Where($"ael.url = ANY({links})");
        }

        if (queryArtist.GetFilters() is null)
        {
            return mIdSongArtists;
            throw new Exception("At least one filter must be applied");
        }

        // Console.WriteLine(queryArtist.Sql);
        await connection.QueryAsync(queryArtist.Sql,
            new[] { typeof(ArtistMusic), typeof(ArtistAlias), typeof(Artist), typeof(ArtistExternalLink) }, (objects) =>
            {
                // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                var artistMusic = (ArtistMusic)objects[0];
                var artistAlias = (ArtistAlias)objects[1];
                var artist = (Artist)objects[2];
                var artistExternalLink = (ArtistExternalLink?)objects[3];

                var title = new Title()
                {
                    LatinTitle = artistAlias.latin_alias,
                    NonLatinTitle = artistAlias.non_latin_alias,
                    IsMainTitle = artistAlias.is_main_name,
                    Language = artist.primary_language ?? "",
                    ArtistAliasId = artistAlias.id,
                };

                if (!mIdSongArtists.TryGetValue(artistMusic.music_id, out var songArtists))
                {
                    Dictionary<int, SongArtist> dict = new();
                    mIdSongArtists.Add(artistMusic.music_id, dict);
                    songArtists = dict;
                }

                if (!songArtists.TryGetValue(artist.id, out var existingArtist))
                {
                    var songArtist = new SongArtist()
                    {
                        Id = artist.id,
                        PrimaryLanguage = artist.primary_language,
                        Titles = new List<Title> { title },
                    };
                    songArtists[artist.id] = songArtist;

                    if (artist.sex.HasValue)
                    {
                        songArtist.Sex = (Sex)artist.sex.Value;
                    }

                    songArtist.Roles = new List<SongArtistRole> { (SongArtistRole)artistMusic.role };

                    if (artistExternalLink is not null)
                    {
                        songArtists[artist.id].Links.Add(new SongArtistLink
                        {
                            Url = artistExternalLink.url,
                            Type = artistExternalLink.type,
                            Name = artistExternalLink.name,
                        });
                    }
                }
                else
                {
                    if (!existingArtist.Titles.Any(x => x.ArtistAliasId == artistAlias.id))
                    {
                        existingArtist.Titles.Add(title);
                    }

                    if (!existingArtist.Roles.Contains((SongArtistRole)artistMusic.role))
                    {
                        existingArtist.Roles.Add((SongArtistRole)artistMusic.role);
                        existingArtist.Roles.Sort(); // todo? only do this once at the end
                    }

                    if (artistExternalLink is not null)
                    {
                        if (!existingArtist.Links.Any(x => x.Url == artistExternalLink.url))
                        {
                            songArtists[artist.id].Links.Add(new SongArtistLink
                            {
                                Url = artistExternalLink.url,
                                Type = artistExternalLink.type,
                                Name = artistExternalLink.name,
                            });
                        }
                    }
                }

                return 0;
            },
            splitOn:
            "id,id,artist_id", param: queryArtist.Parameters, transaction: transaction);

        if (needsRequery && mIdSongArtists.Any())
        {
            var inputWithArtistId = mIdSongArtists.SelectMany(x => x.Value.Keys).Select(x =>
                new Song() { Artists = new List<SongArtist>() { new SongArtist() { Id = x } } }).ToList();

            mIdSongArtists = await SelectArtistBatch(connection, inputWithArtistId, false, transaction);
        }

        var aIds = mIdSongArtists.SelectMany(x => x.Value.Keys).ToArray();
        var artistArtists = (await connection.QueryAsync<ArtistArtist>(
            "select distinct * from artist_artist where source = any(@aIds) or target = any(@aIds)",
            new { aIds }, transaction)).ToArray();

        foreach (KeyValuePair<int, Dictionary<int, SongArtist>> mIdSongArtist in mIdSongArtists)
        {
            foreach ((int key, SongArtist value) in mIdSongArtist.Value)
            {
                value.ArtistArtists = artistArtists.Where(x => x.source == key || x.target == key).ToList();
            }
        }

        return mIdSongArtists;
    }

    // todo not actually Batch
    // todo? implement this like selectCategories instead
    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Artists.Id <br/>
    /// Song.Artists.Titles.ArtistAliasId <br/>
    /// Song.Artists.Titles.LatinTitle <br/>
    /// Song.Artists.Titles.NonLatinTitle <br/>
    /// Song.Artists.Links.Url <br/>
    /// </summary>
    public static async Task<Dictionary<int, Dictionary<int, SongArtist>>> SelectArtistBatchNoAM(
        IDbConnection connection,
        List<Song> songs,
        bool needsRequery)
    {
        var mIdSongArtists = new Dictionary<int, Dictionary<int, SongArtist>>();
        var queryArtist = connection
            .QueryBuilder($@"SELECT *
            FROM artist_alias aa
            LEFT JOIN artist a ON a.id = aa.artist_id
            LEFT JOIN artist_external_link ael ON ael.artist_id = aa.artist_id
            /**where**/
    ");

        int?[] artistIds = songs.Select(a => a.Artists.FirstOrDefault()?.Id).Where(x => x is > 0).ToArray();
        if (artistIds.Any())
        {
            queryArtist.Where($"a.id = ANY({artistIds})");
        }

        int[] artistAliasIds = songs.SelectMany(a =>
                a.Artists.FirstOrDefault()?.Titles.Select(x => x.ArtistAliasId) ?? Array.Empty<int>())
            .Where(x => x > 0).ToArray();
        if (artistAliasIds.Any())
        {
            queryArtist.Where($"aa.id = ANY({artistAliasIds})");
        }

        List<string> latinTitles = songs.SelectMany(a => a.Artists.SelectMany(x => x.Titles.Select(y => y.LatinTitle)))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (latinTitles.Any())
        {
            queryArtist.Where($"lower(aa.latin_alias) = ANY(lower({latinTitles}::text)::text[])");
        }

        List<string?> nonLatinTitles = songs
            .SelectMany(a => a.Artists.SelectMany(x => x.Titles.Select(y => y.NonLatinTitle)))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (nonLatinTitles.Any())
        {
            queryArtist.Where($"aa.non_latin_alias = ANY({nonLatinTitles})");
        }

        List<string> links = songs.SelectMany(a => a.Artists.SelectMany(x => x.Links.Select(y => y.Url))).ToList();
        if (links.Any())
        {
            queryArtist.Where($"ael.url = ANY({links})");
        }

        if (queryArtist.GetFilters() is null)
        {
            return mIdSongArtists;
            throw new Exception("At least one filter must be applied");
        }

        // Console.WriteLine(queryArtist.Sql);
        await connection.QueryAsync(queryArtist.Sql,
            new[] { typeof(ArtistAlias), typeof(Artist), typeof(ArtistExternalLink) }, (objects) =>
            {
                // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                var artistAlias = (ArtistAlias)objects[0];
                var artist = (Artist)objects[1];
                var artistExternalLink = (ArtistExternalLink?)objects[2];

                var title = new Title()
                {
                    LatinTitle = artistAlias.latin_alias,
                    NonLatinTitle = artistAlias.non_latin_alias,
                    IsMainTitle = artistAlias.is_main_name,
                    Language = artist.primary_language ?? "",
                    ArtistAliasId = artistAlias.id,
                };

                if (!mIdSongArtists.TryGetValue(-1, out var songArtists))
                {
                    Dictionary<int, SongArtist> dict = new();
                    mIdSongArtists.Add(-1, dict);
                    songArtists = dict;
                }

                if (!songArtists.TryGetValue(artist.id, out var existingArtist))
                {
                    var songArtist = new SongArtist()
                    {
                        Id = artist.id,
                        PrimaryLanguage = artist.primary_language,
                        Titles = new List<Title> { title },
                    };

                    if (artist.sex.HasValue)
                    {
                        songArtist.Sex = (Sex)artist.sex.Value;
                    }

                    songArtists[artist.id] = songArtist;

                    if (artistExternalLink is not null)
                    {
                        songArtists[artist.id].Links.Add(new SongArtistLink
                        {
                            Url = artistExternalLink.url,
                            Type = artistExternalLink.type,
                            Name = artistExternalLink.name,
                        });
                    }
                }
                else
                {
                    if (!existingArtist.Titles.Any(x => x.ArtistAliasId == artistAlias.id))
                    {
                        existingArtist.Titles.Add(title);
                    }

                    if (artistExternalLink is not null)
                    {
                        if (!existingArtist.Links.Any(x => x.Url == artistExternalLink.url))
                        {
                            songArtists[artist.id].Links.Add(new SongArtistLink
                            {
                                Url = artistExternalLink.url,
                                Type = artistExternalLink.type,
                                Name = artistExternalLink.name,
                            });
                        }
                    }
                }

                return 0;
            },
            splitOn:
            "id,artist_id", param: queryArtist.Parameters);

        if (needsRequery && mIdSongArtists.Any())
        {
            var inputWithArtistId = mIdSongArtists.SelectMany(x => x.Value.Keys).Select(x =>
                new Song() { Artists = new List<SongArtist>() { new SongArtist() { Id = x } } }).ToList();

            mIdSongArtists = await SelectArtistBatchNoAM(connection, inputWithArtistId, false);
        }

        var aIds = mIdSongArtists.SelectMany(x => x.Value.Keys).ToArray();
        var artistArtists = (await connection.QueryAsync<ArtistArtist>(
            "select distinct * from artist_artist where source = any(@aIds) or target = any(@aIds)",
            new { aIds })).ToArray();

        foreach (KeyValuePair<int, Dictionary<int, SongArtist>> mIdSongArtist in mIdSongArtists)
        {
            foreach ((int key, SongArtist value) in mIdSongArtist.Value)
            {
                value.ArtistArtists = artistArtists.Where(x => x.source == key || x.target == key).ToList();
            }
        }

        return mIdSongArtists;
    }

    public static async Task<FrozenDictionary<int, string[]>> SelectArtistAliases(int[] aIds)
    {
        string sql =
            @"SELECT artist_id, array_remove(array_cat(array_agg(latin_alias), array_agg(non_latin_alias)), NULL) AS aliases
FROM artist_alias aa
WHERE artist_id = ANY(@aIds)
GROUP BY artist_id";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var res = await connection.QueryAsync<(int aId, string[] aliases)>(sql, new { aIds });
        var dict = res.ToFrozenDictionary(x => x.aId, x => x.aliases.Distinct().ToArray());
        return dict;
    }

    public static async Task<int> InsertSong(Song song, NpgsqlConnection? connection = null,
        NpgsqlTransaction? transaction = null, bool updateMusicTable = false)
    {
        // Console.WriteLine(JsonSerializer.Serialize(song, Utils.Jso));

        bool ownConnection = false;
        if (connection is null)
        {
            ownConnection = true;
            connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            await connection.OpenAsync();
            transaction = await connection.BeginTransactionAsync();
        }

        int mId;
        if (song.Id > 0)
        {
            if (connection.ExecuteScalar<bool>("select 1 from music where id = @id", new { id = song.Id }))
            {
                mId = song.Id;

                // todo? update other stuff
                if (updateMusicTable)
                {
                    int rows = await connection.ExecuteScalarAsync<int>(
                        "UPDATE music SET type = @type, attributes = @attributes WHERE id = @id;",
                        new { type = song.Type, attributes = song.Attributes, id = mId });
                    if (rows < 0)
                    {
                        throw new Exception($"Failed to update music");
                    }
                }
            }
            else
            {
                mId = await connection.ExecuteScalarAsync<int>(
                    "INSERT INTO music (id, type, musicbrainz_recording_gid, attributes, data_source) VALUES (@id, @type, @recgid, @attributes, @datasource) RETURNING id;",
                    new
                    {
                        id = song.Id,
                        type = (int)song.Type,
                        recgid = song.MusicBrainzRecordingGid,
                        attributes = (int)song.Attributes,
                        datasource = song.DataSource
                    }, transaction);
                if (mId != song.Id)
                {
                    throw new Exception($"mId mismatch: expected {song.Id}, got {mId}");
                }
            }
        }
        else
        {
            var music = new Music
            {
                type = song.Type,
                musicbrainz_recording_gid = song.MusicBrainzRecordingGid,
                attributes = song.Attributes,
                data_source = song.DataSource
            };
            if (!await connection.InsertAsync(music, transaction))
            {
                throw new Exception("Failed to insert m");
            }

            mId = music.id;
        }

        foreach (Title songTitle in song.Titles)
        {
            var mt = new MusicTitle()
            {
                music_id = mId,
                latin_title = songTitle.LatinTitle,
                non_latin_title = songTitle.NonLatinTitle,
                language = songTitle.Language,
                is_main_title = songTitle.IsMainTitle
            };

            if (!await connection.InsertAsync(mt, transaction))
            {
                throw new Exception("Failed to insert mt");
            }
        }

        foreach (SongLink songLink in song.Links)
        {
            if (!await connection.InsertAsync(new MusicExternalLink()
                {
                    music_id = mId,
                    url = songLink.Url,
                    type = songLink.Type,
                    is_video = songLink.IsVideo,
                    duration = songLink.Duration,
                    submitted_by = songLink.SubmittedBy,
                    sha256 = songLink.Sha256,
                    analysis_raw = songLink.AnalysisRaw,
                }, transaction))
            {
                throw new Exception("Failed to insert mel");
            }
        }


        int msId = 0;
        foreach (SongSource songSource in song.Sources)
        {
            string msVndbUrl = songSource.Links.Single(y => y.Type == SongSourceLinkType.VNDB).Url;

            if (!string.IsNullOrEmpty(msVndbUrl))
            {
                msId = (await connection.QueryAsync<int>(
                    "select ms.id from music_source_external_link msel join music_source ms on ms.id = msel.music_source_id where msel.url=@mselUrl",
                    new { mselUrl = msVndbUrl }, transaction)).ToList().FirstOrDefault();
            }

            if (msId > 0)
            {
            }
            else
            {
                var ms = new MusicSource()
                {
                    air_date_start = songSource.AirDateStart,
                    air_date_end = songSource.AirDateEnd,
                    language_original = songSource.LanguageOriginal,
                    rating_average = songSource.RatingAverage,
                    rating_bayesian = songSource.RatingBayesian,
                    // popularity = songSource.Popularity,
                    votecount = songSource.VoteCount,
                    type = songSource.Type
                };

                if (!await connection.InsertAsync(ms, transaction))
                {
                    throw new Exception("Failed to insert ms");
                }

                msId = ms.id;

                foreach (Title songSourceAlias in songSource.Titles)
                {
                    if (!await connection.InsertAsync(
                            new MusicSourceTitle()
                            {
                                music_source_id = msId,
                                latin_title = songSourceAlias.LatinTitle,
                                non_latin_title = songSourceAlias.NonLatinTitle,
                                language = songSourceAlias.Language,
                                is_main_title = songSourceAlias.IsMainTitle
                            }, transaction))
                    {
                        throw new Exception("Failed to insert mst");
                    }
                }

                foreach (SongSourceCategory songSourceCategory in songSource.Categories)
                {
                    int cId = (await connection.QueryAsync<int>(
                        "select id from category c where c.vndb_id=@songSourceCategoryVndbId AND c.type=@songSourceCategoryType",
                        new
                        {
                            songSourceCategoryVndbId = songSourceCategory.VndbId,
                            songSourceCategoryType = songSourceCategory.Type
                        }, transaction)).ToList().SingleOrDefault();

                    if (cId > 0)
                    {
                    }
                    else
                    {
                        var newCategory = new Category()
                        {
                            name = songSourceCategory.Name,
                            type = songSourceCategory.Type,
                            vndb_id = songSourceCategory.VndbId,
                        };

                        if (!await connection.InsertAsync(newCategory))
                        {
                            throw new Exception("Failed to insert c");
                        }

                        cId = newCategory.id;
                    }

                    int mscId = (await connection.QueryAsync<int>(
                        "select music_source_id from music_source_category msc where msc.music_source_id=@msId AND msc.category_id =@cId",
                        new { msId, cId }, transaction)).ToList().SingleOrDefault();

                    if (mscId > 0)
                    {
                    }
                    else
                    {
                        await connection.InsertAsync(
                            new MusicSourceCategory()
                            {
                                category_id = cId,
                                music_source_id = msId,
                                rating = songSourceCategory.Rating,
                                spoiler_level = (int?)songSourceCategory.SpoilerLevel,
                            }, transaction);
                    }
                }
            }

            foreach (SongSourceLink songSourceLink in songSource.Links)
            {
                string? url = (await connection.QueryAsync<string>(
                    "select msel.url from music_source_external_link msel where msel.url=@mselUrl and msel.music_source_id=@msId",
                    new { mselUrl = songSourceLink.Url, msId = msId }, transaction)).ToList().FirstOrDefault();

                if (string.IsNullOrEmpty(url))
                {
                    if (!await connection.InsertAsync(
                            new MusicSourceExternalLink()
                            {
                                music_source_id = msId,
                                url = songSourceLink.Url,
                                type = songSourceLink.Type,
                                name = songSourceLink.Name
                            }, transaction))
                    {
                        throw new Exception("Failed to insert msel");
                    }
                }
            }

            foreach (var songSourceSongType in songSource.SongTypes)
            {
                int msmId = (await connection.QueryAsync<int>(
                    "select music_id from music_source_music msm where msm.music_id=@mId AND msm.music_source_id =@msId AND msm.type=@songSourceSongType",
                    new { mId, msId, songSourceSongType }, transaction)).ToList().SingleOrDefault();

                if (msmId > 0)
                {
                }
                else
                {
                    if (!await connection.InsertAsync(
                            new MusicSourceMusic()
                            {
                                music_id = mId, music_source_id = msId, type = songSourceSongType
                            }, transaction))
                    {
                        throw new Exception("Failed to insert msm");
                    }
                }
            }
        }


        foreach (SongArtist songArtist in song.Artists)
        {
            if (songArtist.Titles.Count != 1)
            {
                throw new Exception("Artists must have one artist_alias per song");
            }

            (int aId, List<int> aaIds) = await InsertArtist(songArtist, transaction!);
            int aaId = aaIds.Single();

            foreach (SongArtistRole songArtistRole in songArtist.Roles)
            {
                if (!await connection.InsertAsync(
                        new ArtistMusic()
                        {
                            music_id = mId, artist_id = aId, artist_alias_id = aaId, role = songArtistRole
                        }, transaction))
                {
                    throw new Exception();
                }
            }

            if (aaId < 1)
            {
                throw new Exception("aaId is invalid");
            }
        }

        if (!song.Artists.Any())
        {
            throw new Exception("no artists");
        }

        if (mId < 1)
        {
            throw new Exception("mId is invalid");
        }

        if (msId < 1)
        {
            throw new Exception("msId is invalid");
        }

        if (ownConnection)
        {
            await transaction!.CommitAsync();
            await connection.DisposeAsync();
            await transaction.DisposeAsync();
        }

        Console.WriteLine($"Inserted mId {mId}");
        return mId;
    }

    private static async Task<(int aId, List<int> aaIds)> InsertArtist(SongArtist songArtist,
        IDbTransaction transaction)
    {
        var connection = transaction.Connection!;
        int aId;
        List<int> aaIds = new();

        if (songArtist.Id > 0)
        {
            if (connection.ExecuteScalar<bool>("select 1 from artist where id = @id", new { id = songArtist.Id },
                    transaction))
            {
                aId = songArtist.Id;
                // todo update stuff like primary_language and sex?
            }
            else
            {
                aId = await connection.ExecuteScalarAsync<int>(
                    "INSERT INTO artist (id, sex, primary_language) VALUES (@id, @sex, @primary_language) RETURNING id;",
                    new
                    {
                        id = songArtist.Id,
                        sex = (int)songArtist.Sex,
                        primary_language = songArtist.PrimaryLanguage,
                    }, transaction);
                if (aId != songArtist.Id)
                {
                    throw new Exception($"aId mismatch: expected {songArtist.Id}, got {aId}");
                }
            }
        }
        else
        {
            aId = (await connection.QueryAsync<int>(
                "SELECT artist_id from artist_external_link ael WHERE url = ANY(@urls)",
                new { urls = songArtist.Links.Select(x => x.Url).ToArray() }, transaction)).SingleOrDefault();
            if (aId < 1)
            {
                var artist = new Artist { sex = (int)songArtist.Sex, primary_language = songArtist.PrimaryLanguage, };
                if (!await connection.InsertAsync(artist, transaction))
                {
                    throw new Exception("Failed to insert a");
                }

                aId = artist.id;
            }
        }

        foreach (Title title in songArtist.Titles)
        {
            int aaId = (await connection.QueryAsync<int>("select id from artist_alias where id=@aaId",
                new { aaId = title.ArtistAliasId }, transaction)).ToList().SingleOrDefault();
            if (aaId < 1)
            {
                aaId = (await connection.QueryAsync<int>(
                        @"select aa.id from artist_alias aa join artist a on a.id = aa.artist_id
                        where a.id=@aId AND aa.latin_alias=@latinAlias and ((@nonLatinAlias::text IS NULL) or aa.non_latin_alias = @nonLatinAlias::text)",
                        new { aId, latinAlias = title.LatinTitle, nonLatinAlias = title.NonLatinTitle },
                        transaction))
                    .ToList().SingleOrDefault();
            }

            if (aaId < 1)
            {
                var aa = new ArtistAlias()
                {
                    artist_id = aId,
                    latin_alias = title.LatinTitle,
                    non_latin_alias = title.NonLatinTitle,
                    is_main_name = title.IsMainTitle
                };

                if (!await connection.InsertAsync(aa, transaction))
                {
                    throw new Exception("Failed to insert aa");
                }

                aaId = aa.id;
            }
            else
            {
                var aa = new ArtistAlias()
                {
                    id = aaId,
                    artist_id = aId,
                    latin_alias = title.LatinTitle,
                    non_latin_alias = title.NonLatinTitle,
                    is_main_name = title.IsMainTitle
                };

                if (!await connection.UpsertAsync(aa, transaction))
                {
                    throw new Exception("Failed to upsert aa");
                }
            }

            // todo delete aliases that exist in the db but not in the argument -- might be easier to use UpsertList
            aaIds.Add(aaId);
        }

        foreach (SongArtistLink link in songArtist.Links)
        {
            string? url = (await connection.QueryAsync<string>(
                "select ael.url from artist_external_link ael where ael.url=@aelUrl and ael.artist_id=@aId",
                new { aelUrl = link.Url, aId }, transaction)).ToList().FirstOrDefault();
            if (string.IsNullOrEmpty(url))
            {
                if (!await connection.InsertAsync(
                        new ArtistExternalLink()
                        {
                            artist_id = aId, url = link.Url, type = link.Type, name = link.Name
                        }, transaction))
                {
                    throw new Exception("Failed to insert ael");
                }
            }
        }

        foreach (ArtistArtist arar in songArtist.ArtistArtists)
        {
            if (!await connection.UpsertAsync(arar, transaction))
            {
                throw new Exception("Failed to upsert arar");
            }
        }

        Console.WriteLine($"Inserted aId {aId} aaIds {string.Join(",", aaIds)}");
        return (aId, aaIds);
    }

    // todo take QuizSettings as a required param instead of all this crap
    public static async Task<List<Song>> GetRandomSongs(int numSongs, bool duplicates,
        List<string>? validSources = null, QuizFilters? filters = null, bool printSql = false,
        bool selectCategories = false, List<Player>? players = null, ListDistributionKind? listDistributionKind = null,
        List<int>? validMids = null, List<int>? invalidMids = null,
        Dictionary<SongSourceSongType, int>? songTypesLeft = null, int? ownerUserId = null,
        GamemodeKind? gamemodeKind = null, SongSelectionKind? songSelectionKind = null)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        // 1. Find all valid music ids
        var ret = new List<Song>();

        List<(int, string)> ids = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            string sqlMusicIds =
                $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     WHERE 1=1
                                     AND msel.type={(int)SongSourceLinkType.VNDB}
                                     ";

            var queryMusicIds = connection.QueryBuilder($"{sqlMusicIds:raw}");
            queryMusicIds.AppendLine($"AND mel.type = ANY({SongLink.FileLinkTypes})");

            var excludedArtistIds = new List<int>();
            var excludedCategoryVndbIds = new List<string>();

            // Apply filters
            if (filters != null)
            {
                if (filters.CategoryFilters.Any())
                {
                    Console.WriteLine(
                        $"StartSection GetRandomSongs_categories: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

                    string sqlCategories =
                        $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     JOIN music_source_category msc on msc.music_source_id = ms.id
                                     JOIN category c on c.id = msc.category_id
                                     WHERE 1=1
                                     AND msel.type={(int)SongSourceLinkType.VNDB}
                                     ";

                    var queryCategories = connection.QueryBuilder($"{sqlCategories:raw}");
                    queryCategories.AppendLine($"AND mel.type = ANY({SongLink.FileLinkTypes})");

                    var validCategories = filters.CategoryFilters;
                    var trileans = validCategories.Select(x => x.Trilean);
                    bool hasInclude = trileans.Any(y => y is LabelKind.Include);

                    var ordered = validCategories.OrderByDescending(x => x.Trilean == LabelKind.Include)
                        .ThenByDescending(y => y.Trilean == LabelKind.Maybe)
                        .ThenByDescending(z => z.Trilean == LabelKind.Exclude).ToList();
                    for (int index = 0; index < ordered.Count; index++)
                    {
                        CategoryFilter categoryFilter = ordered[index];
                        // Console.WriteLine("processing c " + categoryFilter.SongSourceCategory.VndbId);

                        if (categoryFilter.Trilean is LabelKind.Exclude)
                        {
                            // needs to be handled at the end
                            excludedCategoryVndbIds.Add(categoryFilter.SongSourceCategory.VndbId ?? "");
                            continue;
                        }

                        if (index > 0)
                        {
                            switch (categoryFilter.Trilean)
                            {
                                case LabelKind.Maybe:
                                    if (hasInclude)
                                    {
                                        continue;
                                    }

                                    queryCategories.AppendLine($"UNION");
                                    break;
                                case LabelKind.Include:
                                    queryCategories.AppendLine($"INTERSECT");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            queryCategories.Append($"{sqlCategories:raw}");
                        }

                        // todo vndb_id null
                        queryCategories.Append(
                            $@" AND c.vndb_id = {categoryFilter.SongSourceCategory.VndbId}
 AND msc.spoiler_level <= {(int?)categoryFilter.SongSourceCategory.SpoilerLevel ?? int.MaxValue}
 AND msc.rating >= {categoryFilter.SongSourceCategory.Rating ?? 0}");
                    }

                    if (printSql)
                    {
                        Console.WriteLine(queryCategories.Sql);
                        Console.WriteLine(JsonSerializer.Serialize(queryCategories.Parameters, Utils.JsoIndented));
                    }

                    var resCategories =
                        (await connection.QueryAsync<(int, string)>(queryCategories.Sql, queryCategories.Parameters))
                        .Shuffle().ToList();
                    ids = resCategories;
                }

                if (filters.ArtistFilters.Any())
                {
                    Console.WriteLine(
                        $"StartSection GetRandomSongs_artists: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

                    string sqlArtists =
                        $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     JOIN artist_music am ON am.music_id = m.id
                                     JOIN artist a ON a.id = am.artist_id
                                     WHERE 1=1
                                     AND msel.type={(int)SongSourceLinkType.VNDB}
                                     ";

                    var queryArtists = connection.QueryBuilder($"{sqlArtists:raw}");
                    queryArtists.AppendLine($"AND mel.type = ANY({SongLink.FileLinkTypes})");

                    var validArtists = filters.ArtistFilters;
                    var trileans = validArtists.Select(x => x.Trilean);
                    bool hasInclude = trileans.Any(y => y is LabelKind.Include);

                    var ordered = validArtists.OrderByDescending(x => x.Trilean == LabelKind.Include)
                        .ThenByDescending(y => y.Trilean == LabelKind.Maybe)
                        .ThenByDescending(z => z.Trilean == LabelKind.Exclude).ToList();
                    for (int index = 0; index < ordered.Count; index++)
                    {
                        ArtistFilter artistFilter = ordered[index];

                        if (artistFilter.Trilean is LabelKind.Exclude)
                        {
                            // needs to be handled at the end
                            excludedArtistIds.Add(artistFilter.Artist.AId);
                            continue;
                        }

                        if (index > 0)
                        {
                            switch (artistFilter.Trilean)
                            {
                                case LabelKind.Maybe:
                                    if (hasInclude)
                                    {
                                        continue;
                                    }

                                    queryArtists.AppendLine($"UNION");
                                    break;
                                case LabelKind.Include:
                                    queryArtists.AppendLine($"INTERSECT");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            queryArtists.Append($"{sqlArtists:raw}");
                        }

                        queryArtists.Append(
                            $@" AND a.id = {artistFilter.Artist.AId}");
                    }

                    if (printSql)
                    {
                        Console.WriteLine(queryArtists.Sql);
                        Console.WriteLine(JsonSerializer.Serialize(queryArtists.Parameters, Utils.JsoIndented));
                    }

                    var resArtist =
                        (await connection.QueryAsync<(int, string)>(queryArtists.Sql, queryArtists.Parameters))
                        .Shuffle().ToList();

                    if (ids.Any())
                    {
                        bool and = true; // todo? option
                        if (and)
                        {
                            ids = ids.Intersect(resArtist).ToList();
                        }
                        else
                        {
                            ids = ids.Union(resArtist).ToList();
                        }
                    }
                    else
                    {
                        ids = resArtist;
                    }
                }

                Console.WriteLine(
                    $"StartSection GetRandomSongs_filters: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

                if (ids.Any())
                {
                    // apply results of category/artist filters
                    queryMusicIds.AppendLine($"AND m.id = ANY({ids.Select(x => x.Item1).ToList()})");
                }

                var validSongDifficultyLevels = filters.SongDifficultyLevelFilters.Where(x => x.Value).ToList();
                if (validSongDifficultyLevels.Any() &&
                    // non-default check
                    (validSongDifficultyLevels.Count != 6 ||
                     validSongDifficultyLevels.Any(x => !x.Value)))
                {
                    queryMusicIds.Append($"\n");
                    for (int index = 0; index < validSongDifficultyLevels.Count; index++)
                    {
                        (SongDifficultyLevel songDifficultyLevel, _) = validSongDifficultyLevels.ElementAt(index);
                        var range = songDifficultyLevel.GetRange();
                        double min = (double)range!.Minimum;
                        double max = (double)range!.Maximum;
                        queryMusicIds.Append(index == 0
                            ? (FormattableString)
                            $" AND (( m.stat_correctpercentage >= {min} AND m.stat_correctpercentage <= {max} )"
                            : (FormattableString)
                            $" OR ( m.stat_correctpercentage >= {min} AND m.stat_correctpercentage <= {max} )");
                    }

                    queryMusicIds.Append($")");
                }

                var validSongSourceSongTypes = new HashSet<SongSourceSongType>();
                foreach ((SongSourceSongType key, IntWrapper? value) in filters.SongSourceSongTypeFilters)
                {
                    if (value.Value > 0 && key != SongSourceSongType.Random)
                    {
                        validSongSourceSongTypes.Add(key);
                    }
                }

                if (filters.SongSourceSongTypeFilters.TryGetValue(SongSourceSongType.Random, out var randomCount) &&
                    randomCount.Value > 0)
                {
                    foreach ((SongSourceSongType key, bool value) in filters.SongSourceSongTypeRandomEnabledSongTypes)
                    {
                        if (value)
                        {
                            validSongSourceSongTypes.Add(key);
                        }
                    }
                }

                if (validSongSourceSongTypes.Any())
                {
                    queryMusicIds.Append($"\n");
                    for (int index = 0; index < validSongSourceSongTypes.Count; index++)
                    {
                        SongSourceSongType songSourceSongType = validSongSourceSongTypes.ElementAt(index);
                        queryMusicIds.Append(index == 0
                            ? (FormattableString)$" AND ( msm.type = {(int)songSourceSongType}"
                            : (FormattableString)$" OR msm.type = {(int)songSourceSongType}");
                    }

                    queryMusicIds.Append($")");
                }

                if (!filters.VNOLangs.ContainsKey(Language.allLanguages) || !filters.VNOLangs[Language.allLanguages])
                {
                    var langs = filters.VNOLangs.Where(x => x.Value)
                        .Select(x => x.Key.GetDescription() ?? x.Key.ToString()).ToList();
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append(
                        $"ms.language_original = ANY({langs})");
                    queryMusicIds.Append($")");
                }

                if (filters.StartDateFilter != DateTime.Parse(Constants.QFDateMin, CultureInfo.InvariantCulture) ||
                    filters.EndDateFilter != DateTime.Parse(Constants.QFDateMax, CultureInfo.InvariantCulture))
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append($"ms.air_date_start >= {filters.StartDateFilter}");
                    queryMusicIds.Append($" AND ms.air_date_start <= {filters.EndDateFilter}");
                    queryMusicIds.Append($")");
                }

                if (filters.RatingAverageStart != Constants.QFRatingAverageMin ||
                    filters.RatingAverageEnd != Constants.QFRatingAverageMax)
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append($"ms.rating_average >= {filters.RatingAverageStart}");
                    queryMusicIds.Append($" AND ms.rating_average <= {filters.RatingAverageEnd}");
                    queryMusicIds.Append($")");
                }

                if (filters.RatingBayesianStart != Constants.QFRatingBayesianMin ||
                    filters.RatingBayesianEnd != Constants.QFRatingBayesianMax)
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append($"ms.rating_bayesian >= {filters.RatingBayesianStart}");
                    queryMusicIds.Append($" AND ms.rating_bayesian <= {filters.RatingBayesianEnd}");
                    queryMusicIds.Append($")");
                }

                // if (filters.PopularityStart != Constants.QFPopularityMin ||
                //     filters.PopularityEnd != Constants.QFPopularityMax)
                // {
                //     queryMusicIds.Append($"\n");
                //     queryMusicIds.Append($" AND (");
                //     queryMusicIds.Append($"ms.popularity >= {filters.PopularityStart}");
                //     queryMusicIds.Append($" AND ms.popularity <= {filters.PopularityEnd}");
                //     queryMusicIds.Append($")");
                // }

                if (filters.VoteCountStart != Constants.QFVoteCountMin ||
                    filters.VoteCountEnd != Constants.QFVoteCountMax)
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append($"ms.votecount >= {filters.VoteCountStart}");
                    queryMusicIds.Append($" AND ms.votecount <= {filters.VoteCountEnd}");
                    queryMusicIds.Append($")");
                }

                if (filters.OnlyOwnUploads && players != null)
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append(
                        $"lower(mel.submitted_by) = ANY({players.Select(x => x.Username.ToLowerInvariant()).ToList()})");
                    queryMusicIds.Append($")");
                }

                if (filters.SongRatingAverageStart != Constants.QFSongRatingAverageMin ||
                    filters.SongRatingAverageEnd != Constants.QFSongRatingAverageMax)
                {
                    // TOSCALE
                    var musicVotes =
                        await connection.QueryAsync<(int mId, float avg)>(
                            @"SELECT music_id, avg(vote) * 10 FROM music_vote where not user_id = any(@ign) GROUP BY music_id",
                            new { ign = IgnoredMusicVotes });

                    var validMidsMusicVotes = musicVotes.Where(x =>
                            x.avg >= filters.SongRatingAverageStart &&
                            x.avg <= filters.SongRatingAverageEnd)
                        .Select(x => x.mId);

                    validMids = validMids == null
                        ? validMidsMusicVotes.ToList()
                        : validMidsMusicVotes.Intersect(validMids).ToList();
                }

                if (filters.OwnersSongRatingAverageStart != Constants.QFSongRatingAverageMin ||
                    filters.OwnersSongRatingAverageEnd != Constants.QFSongRatingAverageMax)
                {
                    var musicVotes =
                        await connection.QueryAsync<(int mId, float avg)>(
                            @"SELECT music_id, avg(vote) * 10 FROM music_vote where user_id = @ownerUserId GROUP BY music_id",
                            new { ownerUserId });

                    var validMidsMusicVotes = musicVotes.Where(x =>
                            x.avg >= filters.OwnersSongRatingAverageStart &&
                            x.avg <= filters.OwnersSongRatingAverageEnd)
                        .Select(x => x.mId);

                    validMids = validMids == null
                        ? validMidsMusicVotes.ToList()
                        : validMidsMusicVotes.Intersect(validMids).ToList();
                }

                switch (filters.OwnersMusicVoteStatus)
                {
                    case MusicVoteStatusKind.All:
                        break;
                    case MusicVoteStatusKind.Voted:
                        {
                            var musicVotes = await connection.QueryAsync<int>(
                                @"SELECT music_id FROM music_vote where user_id = @ownerUserId", new { ownerUserId });
                            validMids = validMids == null
                                ? musicVotes.ToList()
                                : musicVotes.Intersect(validMids).ToList();
                            break;
                        }
                    case MusicVoteStatusKind.Unvoted:
                        {
                            var musicVotes = await connection.QueryAsync<int>(
                                @"SELECT music_id FROM music_vote where user_id = @ownerUserId", new { ownerUserId });
                            invalidMids ??= new List<int>();
                            invalidMids.AddRange(musicVotes);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (filters != null && validSources != null && validSources.Any())
            {
                if (songSelectionKind == SongSelectionKind.Looting || filters.ListReadKindFiltersIsOnlyRead)
                {
                    Console.WriteLine("onlyRead");
                    queryMusicIds.Append($@" AND msel.url = ANY({validSources})");
                }
                else if (filters.ListReadKindFilters.TryGetValue(ListReadKind.Unread, out var ur) && ur.Value > 0)
                {
                    bool onlyUnread = !filters.ListReadKindFilters.Where(x => x.Key != ListReadKind.Unread)
                        .Any(x => x.Value.Value > 0);
                    if (onlyUnread)
                    {
                        Console.WriteLine("onlyUnread");
                        queryMusicIds.Append($@" AND NOT msel.url = ANY({validSources})");
                    }
                }
            }

            if (excludedCategoryVndbIds.Any())
            {
                var allMidsOfExcluded = await connection.QueryAsync<int>(
                    @"select distinct music_id from music_source_music msm
    join music_source_category msc on msm.music_source_id = msc.music_source_id
    join category c on msc.category_id = c.id
    where c.vndb_id = ANY(@excludedCategoryVndbIds)", new { excludedCategoryVndbIds });

                invalidMids ??= new List<int>();
                invalidMids.AddRange(allMidsOfExcluded);
            }

            if (excludedArtistIds.Any())
            {
                var allMidsOfExcluded = await connection.QueryAsync<int>(
                    @"select distinct music_id from artist_music am
    join artist a on am.artist_id = a.id
    where a.id = ANY(@excludedArtistIds)", new { excludedArtistIds });

                invalidMids ??= new List<int>();
                invalidMids.AddRange(allMidsOfExcluded);
            }

            if (validMids != null)
            {
                queryMusicIds.Append($@" AND m.id = ANY({validMids})");
            }

            if (invalidMids != null)
            {
                queryMusicIds.Append($@" AND NOT (m.id = ANY({invalidMids}))");
            }

            // we will get the same ids every time if we do this before randomizing
            // queryMusicIds.AppendLine($@"LIMIT {numSongs}");

            if (printSql)
            {
                Console.WriteLine(queryMusicIds.Sql);
                Console.WriteLine(JsonSerializer.Serialize(queryMusicIds.Parameters, Utils.JsoIndented));
            }

            ids = (await connection.QueryAsync<(int, string)>(queryMusicIds.Sql, queryMusicIds.Parameters))
                .Shuffle().ToList();
            // Console.WriteLine(JsonSerializer.Serialize(ids.Select(x => x.Item1)));

            if (!ids.Any())
            {
                return ret;
            }
        }

        Console.WriteLine(
            $"StartSection GetRandomSongs_SelectSongs: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        // 2. Select Song objects with those music ids until we hit the desired NumSongs, respecting the Duplicates and Song Types settings
        var addedMselUrls = new List<string>();

        bool doSongSourceSongTypeFiltersCheck = filters?.SongSourceSongTypeFilters.Any(x => x.Value.Value > 0) ?? false;
        bool doListReadKindFiltersCheck = songSelectionKind != SongSelectionKind.Looting &&
                                          (filters?.ListReadKindFilters.Any(x => x.Value.Value > 0) ?? false);

        songTypesLeft ??= filters?.SongSourceSongTypeFilters
            .OrderByDescending(x => x.Key) // Random must be selected first
            .Where(x => x.Value.Value > 0)
            .ToDictionary(x => x.Key, x => x.Value.Value);

        // todo? param
        Dictionary<ListReadKind, int>? listReadKindLeft = filters?.ListReadKindFilters
            .OrderByDescending(x => x.Key) // Random must be selected first
            .Where(x => x.Value.Value > 0)
            .ToDictionary(x => x.Key, x => x.Value.Value);

        List<SongSourceSongType>? enabledSongTypesForRandom =
            filters?.SongSourceSongTypeRandomEnabledSongTypes
                .Where(x => x.Value)
                .Select(y => y.Key).ToList();

        Console.WriteLine(
            $"enabledSongTypesForRandom: {JsonSerializer.Serialize(enabledSongTypesForRandom, Utils.Jso)}");

        int lastIndex = 0;
        var songsDict = new Dictionary<int, Song>();
        int totalSelected = 0;
        foreach ((int mId, string? mselUrl) in ids)
        {
            if (ret.Count >= numSongs ||
                songTypesLeft != null && !songTypesLeft.Any(x => x.Value > 0))
            {
                break;
            }

            if (songTypesLeft != null && songTypesLeft.TryGetValue(SongSourceSongType.Random, out int _) &&
                enabledSongTypesForRandom != null && !enabledSongTypesForRandom.Any())
            {
                songTypesLeft[SongSourceSongType.Random] = 0;
            }

            if (!addedMselUrls.Contains(mselUrl) || duplicates)
            {
                bool exists = songsDict.TryGetValue(mId, out Song? song);
                while (!exists)
                {
                    const int chunkSize = 1024;
                    int index = lastIndex * chunkSize;
                    var chunk = ids.GetRange(index, Math.Min(chunkSize, ids.Count - index));
                    var d = (await SelectSongsMIds(chunk.Select(x => x.Item1).ToArray(), false))
                        .ToDictionary(x => x.Id, x => x);
                    songsDict = songsDict.Concat(d).ToDictionary(x => x.Key, x => x.Value);
                    exists = songsDict.TryGetValue(mId, out song);
                    lastIndex += 1;
                    // Console.WriteLine($"lastIndex: {lastIndex}");
                }

                totalSelected += 1;
                bool canAdd = true;
                ListReadKind? listReadKindKey = null;
                SongSourceSongType? songSourceSongTypeKey = null;
                if (doListReadKindFiltersCheck)
                {
                    string[] songSourceVndbUrls = song!.Sources
                        .SelectMany(x => x.Links.Where(y => y.Type == SongSourceLinkType.VNDB))
                        .Select(x => x.Url).ToArray();
                    bool isRead = validSources != null && songSourceVndbUrls.Any(x => validSources.Contains(x));
                    foreach ((ListReadKind key, int value) in listReadKindLeft!)
                    {
                        if (key == ListReadKind.Random || ((key == ListReadKind.Read && isRead) ||
                                                           (key == ListReadKind.Unread && !isRead)))
                        {
                            // Console.WriteLine($"{song.ToStringLatin()} isRead: {isRead} key: {key}");
                            if (value <= 0)
                            {
                                // Console.WriteLine("canAdd = false");
                                canAdd = false;
                                continue;
                            }

                            // Console.WriteLine($"{song.ToStringLatin()} isRead: {isRead} key: {key}");
                            // Console.WriteLine("canAdd = true");
                            canAdd = true;
                            listReadKindKey = key;
                            break;
                        }
                    }

                    // Console.WriteLine("foreach end");
                }

                if (!canAdd)
                {
                    continue;
                }

                if (doSongSourceSongTypeFiltersCheck)
                {
                    SongSourceSongType[] songTypes = song!.Sources.SelectMany(x => x.SongTypes).ToArray();
                    foreach ((SongSourceSongType key, int value) in songTypesLeft!)
                    {
                        if (key == SongSourceSongType.Random || songTypes.Contains(key))
                        {
                            if (value <= 0)
                            {
                                canAdd = false;
                                continue;
                            }

                            if (key == SongSourceSongType.Random &&
                                (enabledSongTypesForRandom != null &&
                                 !songTypes.Any(x => enabledSongTypesForRandom.Contains(x))))
                            {
                                canAdd = false;
                                continue;
                            }

                            if (key == SongSourceSongType.Random && songTypes.Contains(SongSourceSongType.BGM))
                            {
                                const float weight = 7f;
                                if (Random.Shared.NextSingle() >= (weight / 100))
                                {
                                    canAdd = false;
                                    break;
                                }
                            }

                            canAdd = true;
                            songSourceSongTypeKey = key;
                            break;
                        }
                    }
                }

                bool isDuplicate = addedMselUrls.Contains(mselUrl);
                canAdd &= !isDuplicate || duplicates;
                if (canAdd)
                {
                    if (listReadKindKey != null)
                    {
                        listReadKindLeft![listReadKindKey.Value] -= 1;
                    }

                    if (songSourceSongTypeKey != null)
                    {
                        songTypesLeft![songSourceSongTypeKey.Value] -= 1;
                    }

                    if (filters != null)
                    {
                        var songSource = song!.Sources.First();
                        if (filters.ScreenshotKind != ScreenshotKind.None)
                        {
                            song.ScreenshotUrl = await GetRandomScreenshotUrl(songSource, filters.ScreenshotKind);
                        }

                        song.CoverUrl = await GetRandomScreenshotUrl(songSource, ScreenshotKind.VNCover);
                    }

                    song!.StartTime = gamemodeKind == GamemodeKind.Radio ? 0 : song.DetermineSongStartTime(filters);
                    ret.Add(song);
                    addedMselUrls.Add(mselUrl);
                }
            }
        }

        stopWatch.Stop();
        double finishedSeconds = Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2);
        Console.WriteLine(
            $"StartSection GetRandomSongs_fin: {finishedSeconds}s");

        if (finishedSeconds > 5)
        {
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine("GetRandomSongs took over 5 seconds");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        }

        if (filters != null)
        {
            Console.WriteLine($"totalSelected: {totalSelected}");
            Console.WriteLine($"numSongs: {numSongs}");

            foreach ((SongSourceSongType key, IntWrapper? value) in filters.SongSourceSongTypeFilters)
            {
                Console.WriteLine($"{key}: {value.Value}");
            }

            int opCount = ret.Count(song =>
                song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.OP));

            int edCount = ret.Count(song =>
                song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.ED));

            int insCount = ret.Count(song =>
                song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.Insert));

            int bgmCount = ret.Count(song =>
                song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.BGM));

            int otherCount = ret.Count(song =>
                song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.Other));

            Console.WriteLine($"    opCount: {opCount}");
            Console.WriteLine($"    edCount: {edCount}");
            Console.WriteLine($"    insCount: {insCount}");
            Console.WriteLine($"    bgmCount: {bgmCount}");
            Console.WriteLine($"    otherCount: {otherCount}");
        }

        // randomize again just in case
        return ret.Shuffle().ToList();
    }

    public static async Task<string> SelectAutocompleteMst()
    {
        const string sqlAutocompleteMst =
            @"SELECT DISTINCT music_source_id AS msId, mst.latin_title AS mstLatinTitle, COALESCE(mst.non_latin_title, '') AS mstNonLatinTitle,
                '' AS mstLatinTitleNormalized, '' AS mstNonLatinTitleNormalized
            FROM music_source_title mst
            ";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            AutocompleteMst[] res = (await connection.QueryAsync<AutocompleteMst>(sqlAutocompleteMst))
                .Where(x => x != null!)
                .OrderBy(x => x.MSTLatinTitle)
                .ToArray();

            foreach (var re in res)
            {
                re.MSTLatinTitleNormalized = re.MSTLatinTitle.NormalizeForAutocomplete();
                re.MSTNonLatinTitleNormalized = re.MSTNonLatinTitle.NormalizeForAutocomplete();
            }

            string autocomplete = JsonSerializer.Serialize(res, Utils.Jso);
            return autocomplete;
        }
    }

    public static async Task<string> SelectAutocompleteC()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var categories = await connection.GetListAsync<Category>();
            var songSourceCategories = categories.Select(category => new SongSourceCategory()
                {
                    Id = category.id,
                    Name = category.name,
                    VndbId = category.vndb_id,
                    Type = (SongSourceCategoryType)category.type,
                    Rating = null,
                    SpoilerLevel = null
                })
                .ToList();

            string autocomplete = JsonSerializer.Serialize(songSourceCategories, Utils.JsoNotNull);
            return autocomplete;
        }
    }

    public static async Task<string> SelectAutocompleteA()
    {
        const string sqlAutocompleteA =
            @"SELECT DISTINCT a.id, aa.latin_alias, aa.non_latin_alias
            FROM artist_alias aa
            LEFT JOIN artist a ON a.id = aa.artist_id
            ";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var res = (await connection.QueryAsync<(int, string, string?)>(sqlAutocompleteA))
                .Select(x => new AutocompleteA(x.Item1, x.Item2, x.Item3 ?? "")).ToArray();

            foreach (var re in res)
            {
                re.AALatinAliasNormalized = re.AALatinAlias.NormalizeForAutocomplete();
                re.AANonLatinAliasNormalized = re.AANonLatinAlias.NormalizeForAutocomplete();

                // todo handle mb names (, )
                re.AALatinAliasNormalizedReversed = string.Join("", re.AALatinAlias
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Reverse()).NormalizeForAutocomplete();
                re.AANonLatinAliasNormalizedReversed = string.Join("", re.AANonLatinAlias
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Reverse()).NormalizeForAutocomplete();
            }

            string autocomplete = JsonSerializer.Serialize(res, Utils.Jso);
            return autocomplete;
        }
    }

    public static async Task<string> SelectAutocompleteMt()
    {
        const string sqlAutocompleteMt =
            @"SELECT DISTINCT music_id AS mId, mt.latin_title AS mtLatinTitle, '' AS mtLatinTitleNormalized
            FROM music_title mt
            WHERE music_id = ANY(@validMids)
            ";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
            Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
                .GroupBy(x => x.Item1)
                .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

            List<int> validMids = mids
                .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
                .Select(z => z.Key)
                .ToList();

            AutocompleteMt[] res = (await connection.QueryAsync<AutocompleteMt>(sqlAutocompleteMt, new { validMids }))
                .Where(x => x != null!)
                .OrderBy(x => x.MTLatinTitle)
                .ToArray();

            foreach (var re in res)
            {
                re.MTLatinTitleNormalized = re.MTLatinTitle.NormalizeForAutocomplete();
            }

            string autocomplete = JsonSerializer.Serialize(res, Utils.Jso);
            return autocomplete;
        }
    }

    public static async Task<string> SelectAutocompleteDeveloper()
    {
        var res = VnDevelopers.SelectMany(x =>
                x.Value.Select(y =>
                {
                    (string? latinTitle, string? nonLatinTitle) = VndbImporter.VndbTitleToEmqTitle(y.name, y.latin);
                    return new AutocompleteMst(0, latinTitle, nonLatinTitle ?? "",
                        latinTitle.NormalizeForAutocomplete(), nonLatinTitle?.NormalizeForAutocomplete() ?? "");
                }))
            .DistinctBy(x => x.MSTLatinTitle);
        string autocomplete = JsonSerializer.Serialize(res, Utils.Jso);
        return autocomplete;
    }

    public static async Task<IEnumerable<Song>> FindSongsBySongSourceTitle(string songSourceTitle)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songSources = await SelectSongSourceBatch(connection,
                new List<Song>
                {
                    new Song
                    {
                        Sources = new List<SongSource>
                        {
                            new() { Titles = new List<Title> { new() { LatinTitle = songSourceTitle } } }
                        }
                    }
                }, false);

            if (!songSources.Any())
            {
                songSources = await SelectSongSourceBatch(connection,
                    new List<Song>
                    {
                        new Song
                        {
                            Sources = new List<SongSource>
                            {
                                new()
                                {
                                    Titles = new List<Title>
                                    {
                                        new() { NonLatinTitle = songSourceTitle }
                                    }
                                }
                            }
                        }
                    }, false);
            }

            // Console.WriteLine(JsonSerializer.Serialize(songSources, Utils.JsoIndented));
            songs.AddRange(await SelectSongsMIds(songSources.Select(y => y.Key).ToArray(), false));
        }

        // we can get duplicate songs here if two vns are searchable by the same string and they both share a song
        // example: searching for Persona 4: The Ultimax Ultra Suplex Hold returns both
        // Persona 4: The Ultimax Ultra Suplex Hold and Persona 4: The Ultimate in Mayonaka Arena
        // and they share at least one song together
        return songs.DistinctBy(x => x.Id).OrderBy(x => x.Id);
    }

    /// Only searches by LatinTitle for now.
    public static async Task<IEnumerable<Song>> FindSongsBySongTitle(string songTitle)
    {
        var songs = await SelectSongsBatch(
            new List<Song> { new Song { Titles = new List<Title> { new() { LatinTitle = songTitle } } } },
            false);
        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsBySongSourceCategories(
        List<SongSourceCategory> songSourceCategories)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songSources = await SelectSongSourceBatch(connection,
                new List<Song>
                {
                    new Song { Sources = new List<SongSource> { new() { Categories = songSourceCategories } } }
                }, true);

            // Console.WriteLine(JsonSerializer.Serialize(songSources, Utils.JsoIndented));
            songs.AddRange(await SelectSongsMIds(songSources.Select(y => y.Key).ToArray(), true));
        }

        return songs;
    }

    // todo songSourceSongTypes?
    public static async Task<IEnumerable<Song>> FindSongsByArtistTitle(string artistTitle)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songArtists = await SelectArtistBatch(connection,
                new List<Song>
                {
                    new Song
                    {
                        Artists = new List<SongArtist>
                        {
                            new() { Titles = new List<Title> { new() { LatinTitle = artistTitle } } }
                        }
                    }
                }, true);

            // Console.WriteLine(JsonSerializer.Serialize(songArtists, Utils.JsoIndented));
            songs.AddRange(await SelectSongsMIds(songArtists.Select(y => y.Key).ToArray(), false));
        }

        return songs;
    }

    // todo songSourceSongTypes?
    public static async Task<IEnumerable<Song>> FindSongsByArtistId(int artistId)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songArtists = await SelectArtistBatch(connection,
                new List<Song> { new Song { Artists = new List<SongArtist> { new() { Id = artistId } } } }, false);

            // Console.WriteLine(JsonSerializer.Serialize(songArtists, Utils.JsoIndented));
            songs.AddRange(await SelectSongsMIds(songArtists.Select(y => y.Key).ToArray(), false));
        }

        return songs;
    }

    // todo songSourceSongTypes
    public static async Task<IEnumerable<Song>> FindSongsByUploader(string uploader)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql =
                "SELECT DISTINCT music_id from music_external_link where submitted_by ILIKE @uploader AND type = ANY(@types)";

            var mids =
                (await connection.QueryAsync<int>(sql, new { uploader, types = SongLink.FileLinkTypes })).ToList();
            if (mids.Count > 2000)
            {
                // too costly to process + browsers would freeze because there's no pagination right now
                return songs;
            }

            songs.AddRange(await SelectSongsMIds(mids.ToArray(), false));
        }

        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsByYear(int year, SongSourceSongType[] songSourceSongTypes)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql = @"SELECT DISTINCT music_id
FROM music_source_music msm
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
WHERE substring(date_trunc('year', ms.air_date_start)::text, 1, 4)::int = @year
AND msm.type = ANY(@msmType)";

            var mids = (await connection.QueryAsync<int>(sql,
                new { year = year, msmType = songSourceSongTypes.Cast<int>().ToArray() })).ToList();
            if (mids.Count > 2000)
            {
                // too costly to process + browsers would freeze because there's no pagination right now
                return songs;
            }

            songs.AddRange(await SelectSongsMIds(mids.ToArray(), false));
        }

        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsByDifficulty(SongDifficultyLevel difficulty,
        SongSourceSongType[] songSourceSongTypes)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql = @"SELECT DISTINCT m.id
FROM music m
JOIN music_source_music msm on m.id = msm.music_id
WHERE stat_correctpercentage >= @diffMin AND stat_correctpercentage <= @diffMax
AND stat_played > 0 -- 0 play songs have a GR of 0%, we don't want them
AND msm.type = ANY(@msmType)";

            var mids = (await connection.QueryAsync<int>(sql,
                new
                {
                    diffMin = difficulty.GetRange()!.Minimum,
                    diffMax = difficulty.GetRange()!.Maximum,
                    msmType = songSourceSongTypes.Cast<int>().ToArray()
                })).ToList();
            if (mids.Count > 2000)
            {
                // too costly to process + browsers would freeze because there's no pagination right now
                return songs;
            }

            songs.AddRange(await SelectSongsMIds(mids.ToArray(), false));
        }

        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsByWarnings(MediaAnalyserWarningKind[] warnings,
        SongSourceSongType[] songSourceSongTypes)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql = @"SELECT DISTINCT mel.music_id
FROM music_external_link mel
JOIN music_source_music msm on mel.music_id = msm.music_id
WHERE mel.type = ANY(@types) and mel.analysis_raw NOT LIKE '%Warnings"":[]%'
AND msm.type = ANY(@msmType)";

            var mids = (await connection.QueryAsync<int>(sql,
                new { msmType = songSourceSongTypes.Cast<int>().ToArray(), types = SongLink.FileLinkTypes })).ToList();

            var ret = await SelectSongsMIds(mids.ToArray(), false);
            foreach (Song song in ret)
            {
                var filtered = SongLink.FilterSongLinks(song.Links); // todo? option to not filter
                foreach (SongLink songLink in filtered.Where(x => x.IsFileLink))
                {
                    if (songLink.AnalysisRaw?.Warnings.Any(x => warnings.Contains(x)) ?? false)
                    {
                        songs.Add(song);
                        break;
                    }
                }
            }
        }

        return songs;
    }

    public static async Task<bool> InsertSongLink(int mId, SongLink songLink, IDbTransaction? transaction)
    {
        bool success;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var mel = new MusicExternalLink
            {
                music_id = mId,
                url = songLink.Url,
                type = songLink.Type,
                is_video = songLink.IsVideo,
                duration = songLink.Duration,
                submitted_by = songLink.SubmittedBy,
                sha256 = songLink.Sha256,
                analysis_raw = songLink.AnalysisRaw,
            };

            if (mel.duration == default)
            {
                throw new Exception("MusicExternalLink duration cannot be 0.");
            }

            Console.WriteLine(
                $"Attempting to insert MusicExternalLink: " + JsonSerializer.Serialize(mel, Utils.Jso));
            success = await connection.InsertAsync(mel, transaction);
        }

        return success;
    }

    public static async Task<bool> SetSongStats(int mId, SongStats songStats, IDbTransaction? transaction)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var querySongStats = connection.QueryBuilder($@"UPDATE music SET
                 stat_correct = {songStats.TimesCorrect},
                 stat_played = {songStats.TimesPlayed},
                 stat_guessed = {songStats.TimesGuessed},
                 stat_totalguessms = {songStats.TotalGuessMs}
WHERE id = {mId};
                 ");

            Console.WriteLine(
                $"Attempting to set SongStats for mId {mId}: " + JsonSerializer.Serialize(songStats, Utils.Jso));
            return await connection.ExecuteAsync(querySongStats.Sql, querySongStats.Parameters, transaction) > 0;
        }
    }

    public static async Task<bool> RecalculateSongStats(HashSet<int> mIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            foreach (int mId in mIds)
            {
                string sql =
                    $@"select count(sq.is_correct) filter(where sq.is_correct) as correct, count(sq.music_id) as played, count(sq.guessed) as guessed, sum(sq.first_guess_ms) as totalguessms, count(distinct sq.user_id) as uniqueusers
from (
select qsh.music_id, qsh.user_id, qsh.is_correct, qsh.first_guess_ms, NULLIF(qsh.guess, '') as guessed, row_number() over (partition by qsh.user_id order by qsh.played_at desc) as row_number
from quiz q
join quiz_song_history qsh on qsh.quiz_id = q.id
where q.should_update_stats and music_id = @mId
order by qsh.played_at desc
) sq
where row_number <= {Constants.SHUseLastNPlaysPerPlayer}";

                var res =
                    await connection
                        .QuerySingleAsync<(int correct, int played, int guessed, int totalguessms, int uniqueusers)>(
                            sql,
                            new { mId = mId });

                var querySongStats = connection.QueryBuilder($@"UPDATE music SET
                 stat_correct = {res.correct},
                 stat_played = {res.played},
                 stat_guessed = {res.guessed},
                 stat_totalguessms = {res.totalguessms},
                 stat_uniqueusers = {res.uniqueusers}
WHERE id = {mId};
                 ");

                Console.WriteLine($"Attempting to recalculate SongStats for mId {mId}");
                await connection.ExecuteAsync(querySongStats.Sql, querySongStats.Parameters, transaction);
            }

            await transaction.CommitAsync();
        }

        foreach ((string key, LibraryStats? _) in CachedLibraryStats)
        {
            CachedLibraryStats[key] = null;
        }

        return true;
    }

    public static async Task<int> InsertReviewQueue(int mId, SongLink songLink, string? analysis = null)
    {
        try
        {
            int rqId;
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                var rq = new ReviewQueue()
                {
                    music_id = mId,
                    url = songLink.Url,
                    type = songLink.Type,
                    is_video = songLink.IsVideo,
                    submitted_by = songLink.SubmittedBy!,
                    submitted_on = DateTime.UtcNow,
                    status = (int)ReviewQueueStatus.Pending,
                    reason = null,
                    sha256 = songLink.Sha256,
                };

                rq.analysis_raw ??= songLink.AnalysisRaw;
                if (!string.IsNullOrWhiteSpace(analysis))
                {
                    rq.analysis = analysis;
                }

                bool success = await connection.InsertAsync(rq);
                rqId = rq.id;
                if (rqId > 0)
                {
                    Console.WriteLine($"Inserted ReviewQueue: " + JsonSerializer.Serialize(rq, Utils.Jso));
                }
            }

            return rqId;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return -1;
        }
    }

    public static async Task<int> InsertSongReport(SongReport songReport)
    {
        try
        {
            int reportId;
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                var report = new Report
                {
                    music_id = songReport.music_id,
                    url = songReport.url,
                    report_kind = songReport.report_kind,
                    submitted_by = songReport.submitted_by,
                    submitted_on = DateTime.UtcNow,
                    status = (int)ReviewQueueStatus.Pending,
                    note_user = songReport.note_user,
                };

                bool success = await connection.InsertAsync(report);
                reportId = report.id;
                if (reportId > 0)
                {
                    Console.WriteLine($"Inserted Report: " + JsonSerializer.Serialize(report, Utils.Jso));
                }
            }

            return reportId;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return -1;
        }
    }

    public static async Task<string> ExportSong()
    {
        var songs = await GetRandomSongs(int.MaxValue, true);
        return JsonSerializer.Serialize(songs, Utils.JsoIndented);
    }

    public static async Task<string> ExportSongLite()
    {
        var songs = new List<Song>();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
            Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
                .GroupBy(x => x.Item1)
                .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

            List<int> validMids = mids
                .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
                .Select(z => z.Key)
                .ToList();

            songs.AddRange(await SelectSongsMIds(validMids.ToArray(), false));
        }

        var songLite = songs.Select(song => song.ToSongLite()).ToList();

        HashSet<string> md5Hashes = new();
        foreach (SongLite sl in songLite)
        {
            foreach (SongLink songLink in sl.Links.Where(x => x.Type == SongLinkType.Self))
            {
                songLink.Url = songLink.Url.UnReplaceSelfhostLink();
            }

            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sl));
            byte[] hash = MD5.HashData(bytes);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            if (!md5Hashes.Add(encoded))
            {
                throw new Exception("Duplicate SongLite detected");
            }

            // if (!sl.Links.Any())
            // {
            //     throw new Exception("SongLite must have at least one link to export.");
            // }

            if (sl.SongStats?.TimesPlayed <= 0)
            {
                sl.SongStats = null;
            }
        }

        return JsonSerializer.Serialize(songLite, Utils.JsoIndented);
    }

    public static async Task<string> ExportSongLite_MB()
    {
        var songs = new List<Song>();
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

            songs.AddRange(await SelectSongsMIds(validMids.ToArray(), false));
        }

        var songLite = songs.Select(song => song.ToSongLite_MB()).ToList();

        HashSet<string> md5Hashes = new();
        foreach (SongLite_MB sl in songLite)
        {
            foreach (SongLink songLink in sl.Links)
            {
                songLink.Url = songLink.Url.UnReplaceSelfhostLink();
            }

            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sl));
            byte[] hash = MD5.HashData(bytes);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            if (!md5Hashes.Add(encoded))
            {
                throw new Exception("Duplicate SongLite detected");
            }

            // if (!sl.Links.Any())
            // {
            //     throw new Exception("SongLite must have at least one link to export.");
            // }

            if (sl.SongStats?.TimesPlayed <= 0)
            {
                sl.SongStats = null;
            }
        }

        return JsonSerializer.Serialize(songLite, Utils.JsoIndented);
    }

    public static async Task<string> ExportReviewQueue()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var reviewQueue = (await connection.GetListAsync<ReviewQueue>()).OrderBy(x => x.id).ToList();
            return JsonSerializer.Serialize(reviewQueue, Utils.JsoIndented);
        }
    }

    public static async Task<string> ExportReport()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var reviewQueue = (await connection.GetListAsync<Report>()).OrderBy(x => x.id).ToList();
            return JsonSerializer.Serialize(reviewQueue, Utils.JsoIndented);
        }
    }

    // public static async Task ImportSongLite(List<SongLite> songLites)
    // {
    //     throw new Exception("ded because of artist vndbid changes");
    //
    //     bool b = false;
    //     if (!b)
    //     {
    //         throw new Exception("use ImportVndbData_InsertPendingSongsWithSongLiteMusicIds instead");
    //     }
    //
    //     const string sqlMIdFromSongLite = @"
    //         SELECT DISTINCT m.id
    //         FROM music m
    //         LEFT JOIN music_title mt ON mt.music_id = m.id
    //         LEFT JOIN music_external_link mel ON mel.music_id = m.id
    //         LEFT JOIN music_source_music msm ON msm.music_id = m.id
    //         LEFT JOIN music_source ms ON ms.id = msm.music_source_id
    //         LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
    //         LEFT JOIN artist_music am ON am.music_id = m.id
    //         LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
    //         LEFT JOIN artist a ON a.id = aa.artist_id
    //         WHERE lower(mt.latin_title) = ANY(lower(@mtLatinTitle::text)::text[])
    //           AND msel.url = ANY(@mselUrl)
    //           AND a.vndb_id = ANY(@aVndbId)
    //           AND msm.type = ANY(@msmType)
    //           ";
    //
    //     await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
    //     await connection.OpenAsync();
    //     await using (var transaction = await connection.BeginTransactionAsync())
    //     {
    //         bool errored = false;
    //         foreach (SongLite songLite in songLites)
    //         {
    //             // Console.WriteLine(JsonSerializer.Serialize(songLite, Utils.JsoIndented));
    //             // Console.WriteLine(JsonSerializer.Serialize(songLite.Titles.Select(x => x.LatinTitle).ToList(),
    //             //     Utils.JsoIndented));
    //             // Console.WriteLine(JsonSerializer.Serialize(
    //             //     songLite.SourceVndbIds.Select(x => "https://vndb.org/" + x).ToList(), Utils.JsoIndented));
    //             // Console.WriteLine(JsonSerializer.Serialize(songLite.ArtistVndbIds, Utils.JsoIndented));
    //             List<int> mIds = (await connection.QueryAsync<int>(sqlMIdFromSongLite,
    //                     new
    //                     {
    //                         mtLatinTitle = songLite.Titles.Select(x => x.LatinTitle).ToList(),
    //                         mselUrl = songLite.SourceVndbIds.Select(x => x.Key.ToVndbUrl()).ToList(),
    //                         aVndbId = songLite.ArtistVndbIds,
    //                         msmType = songLite.SourceVndbIds.Select(x => x.Value.Select(y => (int)y)).ToList()
    //                     }))
    //                 .ToList();
    //
    //             if (!mIds.Any())
    //             {
    //                 errored = true;
    //                 Console.WriteLine($"No matches for {JsonSerializer.Serialize(songLite, Utils.JsoIndented)}");
    //                 continue;
    //             }
    //
    //             if (mIds.Count > 1)
    //             {
    //                 errored = true;
    //                 Console.WriteLine($"Multiple matches for {JsonSerializer.Serialize(songLite, Utils.JsoIndented)}");
    //                 continue;
    //             }
    //
    //             foreach (int mId in mIds)
    //             {
    //                 foreach (SongLink link in songLite.Links)
    //                 {
    //                     await InsertSongLink(mId, link, transaction);
    //                 }
    //
    //                 if (songLite.SongStats != null)
    //                 {
    //                     await SetSongStats(mId, songLite.SongStats, transaction);
    //                 }
    //             }
    //         }
    //
    //         if (errored)
    //         {
    //             await transaction.RollbackAsync();
    //             throw new Exception();
    //         }
    //         else
    //         {
    //             await transaction.CommitAsync();
    //         }
    //     }
    // }

    public static async Task ImportSongLite_MB(List<SongLite_MB> songLites)
    {
        bool b = false;
        if (!b)
        {
            throw new Exception("use ImportMusicBrainzData_InsertPendingSongsWithSongLiteMusicIds instead");
        }

        const string sqlMIdFromSongLite = @"
            SELECT m.id
            FROM music m
            WHERE musicbrainz_recording_gid = @recording
";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            bool errored = false;
            foreach (SongLite_MB songLite in songLites)
            {
                List<int> mIds = (await connection.QueryAsync<int>(sqlMIdFromSongLite,
                        new { recording = songLite.Recording }))
                    .ToList();

                if (!mIds.Any())
                {
                    errored = true;
                    Console.WriteLine($"No matches for {JsonSerializer.Serialize(songLite, Utils.JsoIndented)}");
                    continue;
                }

                if (mIds.Count > 1)
                {
                    errored = true;
                    Console.WriteLine($"Multiple matches for {JsonSerializer.Serialize(songLite, Utils.JsoIndented)}");
                    continue;
                }

                foreach (int mId in mIds)
                {
                    foreach (SongLink link in songLite.Links)
                    {
                        await InsertSongLink(mId, link, transaction);
                    }

                    if (songLite.SongStats != null)
                    {
                        await SetSongStats(mId, songLite.SongStats, transaction);
                    }
                }
            }

            if (errored)
            {
                await transaction.RollbackAsync();
                throw new Exception();
            }
            else
            {
                await transaction.CommitAsync();
            }
        }
    }

    // public static async Task ImportReviewQueue(List<ReviewQueue> reviewQueues)
    // {
    //     await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
    //     await connection.OpenAsync();
    //     await using (var transaction = await connection.BeginTransactionAsync())
    //     {
    //         foreach (ReviewQueue reviewQueue in reviewQueues)
    //         {
    //             await connection.InsertAsync(reviewQueue);
    //         }
    //
    //         await transaction.CommitAsync();
    //     }
    // }

    // todo? limit max range to 2 months or something
    public static async Task<IEnumerable<RQ>> FindRQs(DateTime startDate, DateTime endDate)
    {
        var rqs = new List<RQ>(777);
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var reviewQueues =
                (await connection.QueryAsync<ReviewQueue>(
                    "select * from review_queue where submitted_on >= @startDate AND submitted_on <= @endDate order by id",
                    new { startDate, endDate }))
                .ToList();
            var songs = (await SelectSongsMIdsCached(reviewQueues.Select(x => x.music_id).Distinct().ToArray()))
                .ToDictionary(x => x.Id, x => x);

            foreach (ReviewQueue reviewQueue in reviewQueues)
            {
                var song = songs[reviewQueue.music_id];
                var rq = new RQ
                {
                    id = reviewQueue.id,
                    music_id = reviewQueue.music_id,
                    url = reviewQueue.url.ReplaceSelfhostLink(),
                    type = (SongLinkType)reviewQueue.type,
                    is_video = reviewQueue.is_video,
                    submitted_by = reviewQueue.submitted_by,
                    submitted_on = reviewQueue.submitted_on,
                    status = (ReviewQueueStatus)reviewQueue.status,
                    reason = reviewQueue.reason,
                    analysis = reviewQueue.analysis,
                    Song = song,
                    duration = reviewQueue.duration,
                    analysis_raw = reviewQueue.analysis_raw,
                    sha256 = reviewQueue.sha256,
                };

                rqs.Add(rq);
            }
        }

        return rqs;
    }

    public static async Task<RQ> FindRQ(int rqId)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var reviewQueue = await connection.GetAsync<ReviewQueue>(rqId);
            var song = (await SelectSongsMIdsCached(new[] { reviewQueue.music_id })).Single();

            var rq = new RQ
            {
                id = reviewQueue.id,
                music_id = reviewQueue.music_id,
                url = reviewQueue.url.ReplaceSelfhostLink(),
                type = (SongLinkType)reviewQueue.type,
                is_video = reviewQueue.is_video,
                submitted_by = reviewQueue.submitted_by,
                submitted_on = reviewQueue.submitted_on,
                status = (ReviewQueueStatus)reviewQueue.status,
                reason = reviewQueue.reason,
                analysis = reviewQueue.analysis,
                Song = song,
                duration = reviewQueue.duration,
                analysis_raw = reviewQueue.analysis_raw,
                sha256 = reviewQueue.sha256,
            };

            return rq;
        }
    }

    public static async Task<EditQueue> FindEQ(int eqId)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var editQueue = await connection.GetAsync<EditQueue>(eqId);
            return editQueue;
        }
    }

    public static async Task<IEnumerable<EditQueue>> FindEQs(DateTime startDate, DateTime endDate)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var eqs =
                (await connection.QueryAsync<EditQueue>(
                    "select * from edit_queue where submitted_on >= @startDate AND submitted_on <= @endDate order by id",
                    new { startDate, endDate }))
                .ToList();
            return eqs;
        }
    }

    public static async Task<IEnumerable<SongReport>> FindSongReports(DateTime startDate, DateTime endDate)
    {
        var songReports = new List<SongReport>();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            // todo date filter
            var reports = (await connection.GetListAsync<Report>()).ToList();
            var songs =
                (await SelectSongsMIds(reports.Select(x => x.music_id).ToArray(), false)).ToDictionary(x => x.Id,
                    x => x);

            foreach (Report report in reports)
            {
                var song = songs[report.music_id];
                var songReport = new SongReport()
                {
                    id = report.id,
                    music_id = report.music_id,
                    url = report.url,
                    report_kind = (SongReportKind)report.report_kind,
                    submitted_by = report.submitted_by,
                    submitted_on = report.submitted_on,
                    status = (ReviewQueueStatus)report.status,
                    note_mod = report.note_mod,
                    note_user = report.note_user,
                    Song = song,
                };

                songReports.Add(songReport);
            }
        }

        return songReports.OrderBy(x => x.id);
    }

    public static async Task<bool> UpdateReviewQueueItem(int rqId, ReviewQueueStatus requestedStatus,
        string? reason = null, MediaAnalyserResult? analyserResult = null)
    {
        bool success = false;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            ReviewQueue? rq = await connection.GetAsync<ReviewQueue>(rqId);

            if (rq is null)
            {
                throw new InvalidOperationException($"Could not find rqId {rqId}");
            }

            var currentStatus = (ReviewQueueStatus)rq.status;
            switch (currentStatus)
            {
                case ReviewQueueStatus.Pending:
                case ReviewQueueStatus.Rejected:
                    break;
                case ReviewQueueStatus.Approved:
                    if (requestedStatus is ReviewQueueStatus.Pending or ReviewQueueStatus.Rejected)
                    {
                        const string sql = "DELETE FROM music_external_link WHERE music_id = @music_id AND url = @url";
                        await connection.ExecuteAsync(sql, new { rq.music_id, rq.url });
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (requestedStatus)
            {
                case ReviewQueueStatus.Pending:
                case ReviewQueueStatus.Rejected:
                    break;
                case ReviewQueueStatus.Approved:
                    if (rq.duration == null)
                    {
                        throw new Exception($"Cannot approve item {rq.id} without duration.");
                    }

                    if (string.IsNullOrWhiteSpace(rq.sha256))
                    {
                        throw new Exception($"Cannot approve item {rq.id} without sha256.");
                    }

                    var songLink = new SongLink()
                    {
                        Url = rq.url,
                        Type = (SongLinkType)rq.type,
                        IsVideo = rq.is_video,
                        Duration = rq.duration.Value,
                        SubmittedBy = rq.submitted_by,
                        Sha256 = rq.sha256,
                        AnalysisRaw = rq.analysis_raw,
                    };
                    success = await InsertSongLink(rq.music_id, songLink, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedStatus), requestedStatus, null);
            }

            rq.status = requestedStatus;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                rq.reason = reason;
            }

            if (analyserResult != null)
            {
                string analyserResultStr;
                if (analyserResult.IsValid)
                {
                    analyserResultStr = Constants.AnalysisOkStr;
                }
                else
                {
                    analyserResultStr = string.Join(", ", analyserResult.Warnings.Select(x => x.ToString()));
                }

                rq.analysis = analyserResultStr;
                rq.analysis_raw = analyserResult;
                rq.duration = analyserResult.Duration;
                rq.sha256 = analyserResult.Sha256;
            }

            await connection.UpdateAsync(rq);
            Console.WriteLine($"Updated ReviewQueue: " + JsonSerializer.Serialize(rq, Utils.Jso));

            foreach ((string key, LibraryStats? _) in CachedLibraryStats)
            {
                CachedLibraryStats[key] = null;
            }

            await EvictFromSongsCache(rq.music_id);
        }

        return success;
    }

    public static async Task<bool> UpdateEditQueueItem(NpgsqlTransaction transaction, int eqId,
        ReviewQueueStatus requestedStatus,
        string? reason = null)
    {
        bool success;
        var connection = transaction.Connection;

        EditQueue? eq = await connection.GetAsync<EditQueue>(eqId, transaction);
        if (eq is null)
        {
            throw new InvalidOperationException($"Could not find eqId {eqId}");
        }

        // todo abstraction
        bool isNew = string.IsNullOrEmpty(eq.old_entity_json);
        IEditQueueEntity? entity;
        switch (eq.entity_kind)
        {
            case EntityKind.Song:
                entity = JsonSerializer.Deserialize<Song>(eq.entity_json)!;
                break;
            case EntityKind.SongSource:
                throw new NotImplementedException();
            case EntityKind.SongArtist:
                entity = JsonSerializer.Deserialize<SongArtist>(eq.entity_json)!;
                break;
            case EntityKind.None:
            default:
                throw new ArgumentOutOfRangeException();
        }

        switch (eq.status)
        {
            case ReviewQueueStatus.Pending:
            case ReviewQueueStatus.Rejected:
                break;
            case ReviewQueueStatus.Approved:
                if (requestedStatus is ReviewQueueStatus.Pending or ReviewQueueStatus.Rejected)
                {
                    if (isNew)
                    {
                        switch (eq.entity_kind)
                        {
                            case EntityKind.Song:
                                {
                                    var music = await connection.GetAsync<Music>(entity.Id, transaction);
                                    if (await connection.DeleteAsync(music!, transaction))
                                    {
                                        Console.WriteLine($"deleted music {JsonSerializer.Serialize(music)}");
                                    }
                                    else
                                    {
                                        throw new Exception("failed to delete music");
                                    }

                                    break;
                                }
                            case EntityKind.SongSource:
                                throw new NotImplementedException();
                            case EntityKind.SongArtist:
                                {
                                    var artist = await connection.GetAsync<Artist>(entity.Id, transaction);
                                    if (await connection.DeleteAsync(artist!, transaction))
                                    {
                                        Console.WriteLine($"deleted artist {JsonSerializer.Serialize(artist)}");
                                    }
                                    else
                                    {
                                        throw new Exception("failed to delete artist");
                                    }

                                    break;
                                }
                            case EntityKind.None:
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    else
                    {
                        switch (eq.entity_kind)
                        {
                            case EntityKind.Song:
                                {
                                    var oldEntity = JsonSerializer.Deserialize<Song>(eq.old_entity_json!)!;
                                    success = await OverwriteMusic(entity.Id, oldEntity, false, transaction);
                                    break;
                                }
                            case EntityKind.SongSource:
                                throw new NotImplementedException();
                            case EntityKind.SongArtist:
                                {
                                    var oldEntity = JsonSerializer.Deserialize<SongArtist>(eq.old_entity_json!)!;
                                    success = await OverwriteArtist(entity.Id, oldEntity, false, transaction);
                                    break;
                                }
                            case EntityKind.None:
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        switch (requestedStatus)
        {
            case ReviewQueueStatus.Pending:
            case ReviewQueueStatus.Rejected:
                success = true;
                break;
            case ReviewQueueStatus.Approved:
                if (isNew)
                {
                    switch (eq.entity_kind)
                    {
                        case EntityKind.Song:
                            {
                                int newMid = await InsertSong((Song)entity, connection, transaction);
                                success = newMid > 0 && newMid == entity.Id;
                                break;
                            }
                        case EntityKind.SongSource:
                            throw new NotImplementedException();
                        case EntityKind.SongArtist:
                            {
                                (int aId, _) = await InsertArtist((SongArtist)entity, transaction);
                                success = aId > 0 && aId == entity.Id;
                                break;
                            }
                        case EntityKind.None:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    switch (eq.entity_kind)
                    {
                        case EntityKind.Song:
                            success = await OverwriteMusic(entity.Id, (Song)entity, false, transaction);
                            break;
                        case EntityKind.SongSource:
                            throw new NotImplementedException();
                        case EntityKind.SongArtist:
                            success = await OverwriteArtist(entity.Id, (SongArtist)entity, false, transaction);
                            break;
                        case EntityKind.None:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(requestedStatus), requestedStatus, null);
        }

        eq.status = requestedStatus;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            eq.note_mod = reason;
        }

        success &= await connection.UpdateAsync(eq, transaction);
        Console.WriteLine($"Updated EditQueue: " + JsonSerializer.Serialize(eq, Utils.Jso));
        if (eq.entity_kind == EntityKind.Song)
        {
            await EvictFromSongsCache(((Song)entity).Id);
        }
        else
        {
            // todo eject from cache all songs connected to this entity
        }

        foreach ((string key, LibraryStats? _) in CachedLibraryStats)
        {
            CachedLibraryStats[key] = null;
        }

        if (success)
        {
            await RefreshAutocompleteFiles();
        }

        return success;
    }

    public static async Task<LibraryStats> SelectLibraryStats(int limit, SongSourceSongType[] songSourceSongTypes)
    {
        var stopWatch = new Utils.MyStopwatch();
        bool useStopWatch = false;
        if (useStopWatch)
        {
            stopWatch.Start();
        }

        stopWatch.StartSection("start");
        string cacheKey = $"{limit}-{string.Join(",", songSourceSongTypes.OrderBy(x => x).Select(y => y))}";
        if (CachedLibraryStats.TryGetValue(cacheKey, out LibraryStats? cached))
        {
            if (cached != null)
            {
                return cached.Value;
            }
        }

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
            Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
                .GroupBy(x => x.Item1)
                .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

            List<int> validMids = mids
                .Where(x => x.Value.Any(y => songSourceSongTypes.Contains(y)))
                .Select(z => z.Key)
                .ToList();

            stopWatch.StartSection("Song");
            string sqlMusic =
                $"SELECT COUNT(DISTINCT m.id) FROM music m LEFT JOIN music_external_link mel ON mel.music_id = m.id WHERE m.id = ANY(@validMids)";

            string sqlMusicSource =
                $"SELECT COUNT(DISTINCT ms.id) FROM music_source_music msm LEFT JOIN music_source ms ON ms.id = msm.music_source_id LEFT JOIN music_external_link mel ON mel.music_id = msm.music_id WHERE msm.music_id = ANY(@validMids)";

            string sqlArtist =
                $"SELECT COUNT(DISTINCT a.id) FROM artist_music am LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id LEFT JOIN artist a ON a.id = aa.artist_id LEFT JOIN music_external_link mel ON mel.music_id = am.music_id WHERE am.music_id = ANY(@validMids)";

            const string sqlAndClause = $" AND mel.url is not null AND mel.type = ANY(@types)";


            int totalMusicCount =
                await connection.QuerySingleAsync<int>(sqlMusic, new { validMids });
            int availableMusicCount =
                await connection.QuerySingleAsync<int>(sqlMusic + sqlAndClause,
                    new { validMids, types = SongLink.FileLinkTypes });

            int totalMusicSourceCount =
                await connection.QuerySingleAsync<int>(sqlMusicSource, new { validMids });
            int availableMusicSourceCount =
                await connection.QuerySingleAsync<int>(sqlMusicSource + sqlAndClause,
                    new { validMids, types = SongLink.FileLinkTypes });

            int totalArtistCount =
                await connection.QuerySingleAsync<int>(sqlArtist, new { validMids });
            int availableArtistCount =
                await connection.QuerySingleAsync<int>(sqlArtist + sqlAndClause,
                    new { validMids, types = SongLink.FileLinkTypes });

            string fileLinkTypesStr = string.Join(',', SongLink.FileLinkTypes);
            string sqlMusicType =
                @"SELECT msm.type as Type, COUNT(DISTINCT m.id) as MusicCount
FROM music m
LEFT JOIN music_external_link mel ON mel.music_id = m.id
LEFT JOIN music_source_music msm ON msm.music_id = m.id
/**where**/
group by msm.type
order by type";
            var qMusicType = connection.QueryBuilder($"{sqlMusicType:raw}");
            qMusicType.Where($"msm.music_id = ANY({validMids})");
            stopWatch.StartSection("totalMusicTypeCount");
            var totalMusicTypeCount = (await qMusicType.QueryAsync<LibraryStatsMusicType>()).ToList();
            qMusicType.Where($"mel.url is not null");
            qMusicType.Where($"mel.type IN ({fileLinkTypesStr:raw})");

            stopWatch.StartSection("availableMusicTypeCount");
            var availableMusicTypeCount = (await qMusicType.QueryAsync<LibraryStatsMusicType>()).ToList();
            stopWatch.StartSection("mels");
            int videoLinkCount = (await connection.ExecuteScalarAsync<int>(
                "SELECT count(distinct music_id) FROM music_external_link where is_video AND type = ANY(@types) and music_id not in (select music_id FROM music_external_link where not is_video AND type = ANY(@types)) and music_id = ANY(@validMids)",
                new { validMids, types = SongLink.FileLinkTypes }));
            int soundLinkCount = (await connection.ExecuteScalarAsync<int>(
                "SELECT count(distinct music_id) FROM music_external_link where not is_video AND type = ANY(@types) and music_id not in (select music_id FROM music_external_link where is_video AND type = ANY(@types)) and music_id = ANY(@validMids)",
                new { validMids, types = SongLink.FileLinkTypes }));
            int bothLinkCount = (await connection.ExecuteScalarAsync<int>(
                "SELECT count(distinct music_id) FROM music_external_link where is_video AND type = ANY(@types) and music_id in (select music_id FROM music_external_link where not is_video AND type = ANY(@types)) and music_id = ANY(@validMids)",
                new { validMids, types = SongLink.FileLinkTypes }));

            int composerCount = (await connection.ExecuteScalarAsync<int>(
                $"SELECT count(distinct music_id) FROM artist_music am WHERE am.role = {(int)SongArtistRole.Composer} and music_id = ANY(@validMids)",
                new { validMids }));
            int arrangerCount = (await connection.ExecuteScalarAsync<int>(
                $"SELECT count(distinct music_id) FROM artist_music am WHERE am.role = {(int)SongArtistRole.Arranger} and music_id = ANY(@validMids)",
                new { validMids }));
            int lyricistCount = (await connection.ExecuteScalarAsync<int>(
                $"SELECT count(distinct music_id) FROM artist_music am WHERE am.role = {(int)SongArtistRole.Lyricist} and music_id = ANY(@validMids)",
                new { validMids }));

            stopWatch.StartSection("SelectLibraryStats_VN");
            (List<LibraryStatsMsm> _, List<LibraryStatsMsm> msmAvailable) =
                await SelectLibraryStats_VN(connection, limit, songSourceSongTypes, fileLinkTypesStr);

            // todo important do this in a single query
            stopWatch.StartSection("SelectLibraryStats_Artist");
            (List<LibraryStatsAm> _, List<LibraryStatsAm> amAvailable) =
                await SelectLibraryStats_Artist(connection, limit, songSourceSongTypes, fileLinkTypesStr,
                    null);
            (List<LibraryStatsAm> _, List<LibraryStatsAm> amAvailableUnknown) =
                await SelectLibraryStats_Artist(connection, limit, songSourceSongTypes, fileLinkTypesStr,
                    SongArtistRole.Unknown);
            (List<LibraryStatsAm> _, List<LibraryStatsAm> amAvailableVocals) =
                await SelectLibraryStats_Artist(connection, limit, songSourceSongTypes, fileLinkTypesStr,
                    SongArtistRole.Vocals);
            (List<LibraryStatsAm> _, List<LibraryStatsAm> amAvailableComposer) =
                await SelectLibraryStats_Artist(connection, limit, songSourceSongTypes, fileLinkTypesStr,
                    SongArtistRole.Composer);
            (List<LibraryStatsAm> _, List<LibraryStatsAm> amAvailableArranger) =
                await SelectLibraryStats_Artist(connection, limit, songSourceSongTypes, fileLinkTypesStr,
                    SongArtistRole.Arranger);
            (List<LibraryStatsAm> _, List<LibraryStatsAm> amAvailableLyricist) =
                await SelectLibraryStats_Artist(connection, limit, songSourceSongTypes, fileLinkTypesStr,
                    SongArtistRole.Lyricist);

            var amAvailableDict = new Dictionary<string, List<LibraryStatsAm>>()
            {
                { "All", amAvailable },
                { SongArtistRole.Unknown.ToString(), amAvailableUnknown },
                { SongArtistRole.Vocals.ToString(), amAvailableVocals },
                { SongArtistRole.Composer.ToString(), amAvailableComposer },
                { SongArtistRole.Arranger.ToString(), amAvailableArranger },
                { SongArtistRole.Lyricist.ToString(), amAvailableLyricist },
            };

            string sqlMsYear =
                @"SELECT date_trunc('year', ms.air_date_start) AS year, Count(DISTINCT m.id)
FROM music m
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
LEFT JOIN music_external_link mel ON mel.music_id = m.id
/**where**/
group by year
order by year";
            var qMsYear = connection.QueryBuilder($"{sqlMsYear:raw}");
            qMsYear.Where($"msm.music_id = ANY({validMids})");
            stopWatch.StartSection("msYear");
            var msYear =
                (await qMsYear.QueryAsync<(DateTime, int)>()).ToDictionary(x => x.Item1, x => x.Item2);

            qMsYear.Where($"mel.url is not null");
            qMsYear.Where($"mel.type IN ({fileLinkTypesStr:raw})");
            stopWatch.StartSection("msYearAvailable");
            var msYearAvailable =
                (await qMsYear.QueryAsync<(DateTime, int)>()).ToDictionary(x => x.Item1, x => x.Item2);

            if (msYear.Keys.Count > msYearAvailable.Keys.Count)
            {
                foreach ((DateTime key, _) in msYear)
                {
                    if (!msYearAvailable.ContainsKey(key))
                    {
                        msYearAvailable.Add(key, 0);
                    }
                }

                msYearAvailable = msYearAvailable.OrderBy(x => x.Key.Year).ToDictionary(x => x.Key, x => x.Value);
            }


            // Console.WriteLine(
            //     $"StartSection uploaderCounts: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
            var uploaderCountsTotal = (await connection.QueryAsync<(string, int)>($@"
select lower(submitted_by), count(music_id) from music_external_link mel
where submitted_by is not null
and mel.music_id = ANY(@validMids)
and mel.type = {(int)SongLinkType.Self}
group by lower(submitted_by)
order by count(music_id) desc
", new { validMids })).Take(limit).ToDictionary(x => x.Item1, x => x.Item2);

            var uploaderCountsVideo = (await connection.QueryAsync<(string, int)>($@"
select lower(submitted_by), count(music_id) from music_external_link mel
where submitted_by is not null
and mel.is_video
and mel.music_id = ANY(@validMids)
and mel.type = {(int)SongLinkType.Self}
group by lower(submitted_by)
order by count(music_id) desc
", new { validMids })).Take(limit).ToDictionary(x => x.Item1, x => x.Item2);

            var uploaderCountsSound = (await connection.QueryAsync<(string, int)>($@"
select lower(submitted_by), count(music_id) from music_external_link mel
where submitted_by is not null
and not mel.is_video
and mel.music_id = ANY(@validMids)
and mel.type = {(int)SongLinkType.Self}
group by lower(submitted_by)
order by count(music_id) desc
", new { validMids })).Take(limit).ToDictionary(x => x.Item1, x => x.Item2);

            Dictionary<string, UploaderStats> uploaderCounts = new();
            foreach ((string key, int totalCount) in uploaderCountsTotal)
            {
                UploaderStats uploaderStats = new() { TotalCount = totalCount };
                if (uploaderCountsVideo.TryGetValue(key, out int videoCount))
                {
                    uploaderStats.VideoCount = videoCount;
                }

                if (uploaderCountsSound.TryGetValue(key, out int soundCount))
                {
                    uploaderStats.SoundCount = soundCount;
                }

                uploaderCounts[key] = uploaderStats;
            }

            var editorCounts = (await connection.QueryAsync<(string, int)>($@"
select lower(submitted_by), count(*) from edit_queue eq
where eq.status = {(int)ReviewQueueStatus.Approved}
--and mel.music_id = ANY(@validMids) -- todo?
group by lower(submitted_by)
order by count(*) desc
", new { validMids })).Take(limit).ToDictionary(x => x.Item1, x => x.Item2);


            stopWatch.StartSection("songDifficultyLevels");
            var songDifficultyLevels = await GetSongDifficultyLevelCounts(validMids.ToArray());

            // todo batch
            stopWatch.StartSection("warningsDict");
            var warningsDict = new Dictionary<MediaAnalyserWarningKind, int>();
            foreach (MediaAnalyserWarningKind warningKind in Enum.GetValues<MediaAnalyserWarningKind>())
            {
                warningsDict[warningKind] =
                    (await FindSongsByWarnings(new[] { warningKind }, songSourceSongTypes)).Count();
            }

            stopWatch.StartSection("mv");
            var mvAvg = (await connection.QueryAsync<int>(
                @"SELECT music_id FROM music_vote mv
WHERE music_id = ANY(@validMids)
and not user_id = any(@ign)
GROUP BY music_id
HAVING count(*) >= 3
ORDER BY avg(vote) DESC
LIMIT 25", new { validMids, ign = IgnoredMusicVotes }));

            var mvCount = (await connection.QueryAsync<int>(
                @"SELECT music_id FROM music_vote mv
WHERE music_id = ANY(@validMids)
and not user_id = any(@ign)
GROUP BY music_id
ORDER BY count(*) DESC
LIMIT 25", new { validMids, ign = IgnoredMusicVotes }));

            var highlyRatedSongs = (await SelectSongsMIds(mvAvg.ToArray(), false))
                .OrderByDescending(x => x.VoteAverage).ThenByDescending(x => x.VoteCount).ToArray();
            var mostVotedSongs = (await SelectSongsMIds(mvCount.ToArray(), false))
                .OrderByDescending(x => x.VoteCount).ThenByDescending(x => x.VoteAverage).ToArray();

            stopWatch.StartSection("new LibraryStats");
            var libraryStats = new LibraryStats
            {
                // General
                TotalMusicCount = totalMusicCount,
                AvailableMusicCount = availableMusicCount,
                TotalMusicSourceCount = totalMusicSourceCount,
                AvailableMusicSourceCount = availableMusicSourceCount,
                TotalArtistCount = totalArtistCount,
                AvailableArtistCount = availableArtistCount,

                // Song
                TotalLibraryStatsMusicType = totalMusicTypeCount,
                AvailableLibraryStatsMusicType = availableMusicTypeCount,
                VideoLinkCount = videoLinkCount,
                SoundLinkCount = soundLinkCount,
                BothLinkCount = bothLinkCount,
                AvailableComposerCount = composerCount,
                AvailableArrangerCount = arrangerCount,
                AvailableLyricistCount = lyricistCount,

                // VN
                msmAvailable = msmAvailable,

                // Artist
                amAvailableDict = amAvailableDict,

                // VN year
                msYear = msYear,
                msYearAvailable = msYearAvailable,

                // Uploaders & Editors
                UploaderCounts = uploaderCounts,
                EditorCounts = editorCounts,

                // Song difficulty
                SongDifficultyLevels = songDifficultyLevels,

                // Warnings
                Warnings = warningsDict,

                // Song rating
                HighlyRatedSongs = highlyRatedSongs,
                MostVotedSongs = mostVotedSongs,
            };

            stopWatch.Stop();
            CachedLibraryStats[cacheKey] = libraryStats;
            return libraryStats;
        }
    }

    public static async Task<(List<LibraryStatsMsm> msm, List<LibraryStatsMsm> msmAvailable)> SelectLibraryStats_VN(
        IDbConnection connection, int limit, IEnumerable<SongSourceSongType> songSourceSongTypes,
        string fileLinkTypesStr)
    {
        string sqlMusicSourceMusic =
            @"SELECT ms.id AS MSId, mst.latin_title AS MstLatinTitle, msel.url AS MselUrl, COUNT(DISTINCT m.id) AS MusicCount
FROM music m
LEFT JOIN music_external_link mel ON mel.music_id = m.id
LEFT JOIN music_title mt ON mt.music_id = m.id
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
LEFT JOIN music_source_title mst ON mst.music_source_id = ms.id
/**where**/
group by ms.id, mst.latin_title, msel.url ORDER BY COUNT(DISTINCT m.id) desc";

        var qMsm = connection.QueryBuilder($"{sqlMusicSourceMusic:raw}");
        qMsm.Where($"mst.is_main_title = true");
        qMsm.Where($"msel.type = {(int)SongSourceLinkType.VNDB}");
        qMsm.Where($"msm.type = ANY({songSourceSongTypes.Cast<int>().ToArray()})");

        // Console.WriteLine(
        //     $"StartSection msm: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        var msm = (await qMsm.QueryAsync<LibraryStatsMsm>()).ToList();

        qMsm.Where($"mel.url is not null");
        qMsm.Where($"mel.type IN ({fileLinkTypesStr:raw})");
        qMsm.Append($"LIMIT {limit:raw}");
        // Console.WriteLine(
        //     $"StartSection msmAvailable: {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");
        var msmAvailable = (await qMsm.QueryAsync<LibraryStatsMsm>()).ToList();

        for (int index = 0; index < msmAvailable.Count; index++)
        {
            LibraryStatsMsm msmA = msmAvailable[index];

            msmA.AvailableMusicCount = msmA.MusicCount;
            msmA.MusicCount = msm.Where(x => x.MstLatinTitle == msmA.MstLatinTitle).Sum(x => x.MusicCount);
        }

        return (msm, msmAvailable);
    }

    public static async Task<(List<LibraryStatsAm> am, List<LibraryStatsAm> amAvailable)> SelectLibraryStats_Artist(
        IDbConnection connection, int limit, IEnumerable<SongSourceSongType> songSourceSongTypes,
        string fileLinkTypesStr, SongArtistRole? role)
    {
        string sqlArtistMusic =
            @"SELECT a.id AS AId, COUNT(DISTINCT m.id) AS MusicCount, json_agg(DISTINCT ael.*) as LinksJson
FROM music m
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_external_link mel ON mel.music_id = m.id
LEFT JOIN artist_music am ON am.music_id = m.id
LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
LEFT JOIN artist a ON a.id = aa.artist_id
LEFT JOIN artist_external_link ael ON ael.artist_id = aa.artist_id
/**where**/
group by a.id ORDER BY COUNT(DISTINCT m.id) desc";

        var qAm = connection.QueryBuilder($"{sqlArtistMusic:raw}");
        qAm.Where($"msm.type = ANY({songSourceSongTypes.Cast<int>().ToArray()})");
        if (role != null)
        {
            qAm.Where($"am.role = {role}");
        }

        var am = (await qAm.QueryAsync<LibraryStatsAm>()).ToList(); // todo get rid of this
        qAm.Where($"mel.url is not null");
        qAm.Where($"mel.type IN ({fileLinkTypesStr:raw})");
        qAm.Append($"LIMIT {limit:raw}");
        var amAvailable = (await qAm.QueryAsync<LibraryStatsAm>()).ToList();

        var artistAliases = (await connection.QueryAsync<(int aId, string aaLatinAlias, bool aaIsMainName)>(
                "select a.id, aa.latin_alias, aa.is_main_name from artist_alias aa LEFT JOIN artist a ON a.id = aa.artist_id"))
            .ToList();
        var aliasesDict = artistAliases.ToLookup(x => x.aId, x => x);

        Dictionary<int, ArtistExternalLink[]> aelsDict = am.Where(x => x.LinksJson != null && x.LinksJson != "[null]")
            .Select(x => JsonSerializer.Deserialize<ArtistExternalLink[]>(x.LinksJson!)!)
            .ToDictionary(x => x.First().artist_id, x => x);

        foreach (LibraryStatsAm libraryStatsAm in am)
        {
            libraryStatsAm.LinksJson = null;
            var aliases = aliasesDict[libraryStatsAm.AId].ToArray();

            try
            {
                var mainAlias = aliases.SingleOrDefault(x => x.aaIsMainName);
                libraryStatsAm.AALatinAlias =
                    mainAlias != default ? mainAlias.aaLatinAlias : aliases.First().aaLatinAlias;
            }
            catch (Exception)
            {
                Console.WriteLine($"aId: {libraryStatsAm.AId}");
                throw;
            }

            if (aelsDict.TryGetValue(libraryStatsAm.AId, out var aels))
            {
                libraryStatsAm.Links = aels
                    .Select(x => new SongArtistLink { Url = x.url, Type = x.type, Name = x.name, })
                    .ToList();
            }
        }

        foreach (LibraryStatsAm libraryStatsAm in amAvailable)
        {
            libraryStatsAm.LinksJson = null;
            var aliases = aliasesDict[libraryStatsAm.AId].ToArray();
            var mainAlias = aliases.SingleOrDefault(x => x.aaIsMainName);
            libraryStatsAm.AALatinAlias =
                mainAlias != default ? mainAlias.aaLatinAlias : aliases.First().aaLatinAlias;

            if (aelsDict.TryGetValue(libraryStatsAm.AId, out var aels))
            {
                libraryStatsAm.Links = aels
                    .Select(x => new SongArtistLink { Url = x.url, Type = x.type, Name = x.name, })
                    .ToList();
            }
        }

        for (int index = 0; index < amAvailable.Count; index++)
        {
            LibraryStatsAm amA = amAvailable[index];

            amA.AvailableMusicCount = amA.MusicCount;
            var match = am.Where(x => x.AId == amA.AId);
            amA.MusicCount = match.Sum(x => x.MusicCount);
        }

        return (am, amAvailable);
    }

    private static async Task<Dictionary<SongDifficultyLevel, int>> GetSongDifficultyLevelCounts(int[] mIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var songDifficultyLevels = (await connection.QueryAsync<(int, int)>(@$"
    select case
    	WHEN stat_correctpercentage  = {((double)SongDifficultyLevel.Impossible.GetRange()!.Minimum).ToString(CultureInfo.InvariantCulture)} 								                                                                                                   THEN 5
    	WHEN stat_correctpercentage >= {((double)SongDifficultyLevel.VeryHard.GetRange()!.Minimum).ToString(CultureInfo.InvariantCulture)}   and stat_correctpercentage <= {((double)SongDifficultyLevel.VeryHard.GetRange()!.Maximum).ToString(CultureInfo.InvariantCulture)} THEN 4
    	WHEN stat_correctpercentage >= {((double)SongDifficultyLevel.Hard.GetRange()!.Minimum).ToString(CultureInfo.InvariantCulture)}       and stat_correctpercentage <= {((double)SongDifficultyLevel.Hard.GetRange()!.Maximum).ToString(CultureInfo.InvariantCulture)}     THEN 3
    	WHEN stat_correctpercentage >= {((double)SongDifficultyLevel.Medium.GetRange()!.Minimum).ToString(CultureInfo.InvariantCulture)}     and stat_correctpercentage <= {((double)SongDifficultyLevel.Medium.GetRange()!.Maximum).ToString(CultureInfo.InvariantCulture)}   THEN 2
    	WHEN stat_correctpercentage >= {((double)SongDifficultyLevel.Easy.GetRange()!.Minimum).ToString(CultureInfo.InvariantCulture)}       and stat_correctpercentage <= {((double)SongDifficultyLevel.Easy.GetRange()!.Maximum).ToString(CultureInfo.InvariantCulture)}     THEN 1
    	WHEN stat_correctpercentage >= {((double)SongDifficultyLevel.VeryEasy.GetRange()!.Minimum).ToString(CultureInfo.InvariantCulture)}   and stat_correctpercentage <= {((double)SongDifficultyLevel.VeryEasy.GetRange()!.Maximum).ToString(CultureInfo.InvariantCulture)} THEN 0
    	END AS ""diff"",
    count(id)
    from music m
    where stat_played > 0
    and m.id = ANY(@mIds)
    group by diff
    order by diff
    ", new { mIds })).ToDictionary(x => (SongDifficultyLevel)x.Item1, x => x.Item2);
        return songDifficultyLevels;
    }

    public static async Task<int[]> FindMusicIdsByLabels(IEnumerable<Label> reqLabels, SongSourceSongTypeMode ssstm)
    {
        var validSources = Label.GetValidSourcesFromLabels(reqLabels.ToList());
        const string sql = @"SELECT DISTINCT m.id FROM
                                     music m
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     WHERE msel.url = ANY(@validSources)
                                     AND msm.type = ANY(@msmType)
                                     ";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        int[] ids = (await connection.QueryAsync<int>(sql,
            new { validSources, msmType = ssstm.ToSongSourceSongTypes().Cast<int>().ToList() })).ToArray();
        return ids;
    }

    // todo tests
    public static async Task<List<(int, int)>> FindArtistIdsByArtistNames(List<string> artistNames)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            List<string> createrNames = artistNames.ToList();
            foreach (string createrName in artistNames)
            {
                for (int index = 1; index < createrName.Length; index++)
                {
                    string name = createrName;
                    name = name.Insert(index, " ");
                    createrNames.Add(name);
                }
            }

            HashSet<(int, int)> aIds = new();
            foreach (string createrName in createrNames)
            {
                var song = new Song
                {
                    Artists = new List<SongArtist>
                    {
                        new() { Titles = new List<Title> { new() { LatinTitle = createrName } } }
                    }
                };
                var artist = await SelectArtistBatch(connection, new List<Song> { song }, true);
                foreach ((int _, Dictionary<int, SongArtist>? value) in artist)
                {
                    foreach ((int _, SongArtist? songArtist) in value)
                    {
                        aIds.Add((songArtist.Id, songArtist.Titles.Single().ArtistAliasId));
                    }
                }

                // todo? batch this
                song.Artists.Single().Titles.Single().LatinTitle = "";
                song.Artists.Single().Titles.Single().NonLatinTitle = createrName;
                var artist2 = await SelectArtistBatch(connection, new List<Song> { song }, true);
                foreach ((int _, Dictionary<int, SongArtist>? value) in artist2)
                {
                    foreach ((int _, SongArtist? songArtist) in value)
                    {
                        aIds.Add((songArtist.Id, songArtist.Titles.Single().ArtistAliasId));
                    }
                }
            }

            return aIds.ToList();
        }
    }

    public static async Task<HashSet<int>> FindMidsWithSoundLinks()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var mids = (await connection.QueryAsync<int>(
                    "SELECT music_id FROM music_external_link where is_video = false AND type = ANY(@types)",
                    new { types = SongLink.FileLinkTypes }))
                .ToHashSet();
            return mids;
        }
    }

    public static async Task<IEnumerable<Song>> FindSongsByVndbUrl(string vndbUrl)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMusicIds =
                $@"SELECT DISTINCT m.id FROM music m
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id";

            var queryMusicIds = connection.QueryBuilder($"{sqlMusicIds:raw}");
            queryMusicIds.Append($@" AND msel.url = {vndbUrl}");

            var mIds = (await queryMusicIds.QueryAsync<int>()).ToList();

            List<Song> songs = await SelectSongsMIds(mIds.ToArray(), false);
            return songs;
        }
    }

    public static async Task UpdateMusicExternalLinkDuration(string url, TimeSpan resultDuration)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql = @"UPDATE music_external_link SET duration = @resultDuration WHERE url = @url";
            await connection.ExecuteAsync(sql, new { resultDuration, url });
        }
    }

    /// <summary>
    /// Do not use in user-facing code.
    /// </summary>
    public static async Task<int> SelectCountUnsafe(string table, string column = "id")
    {
        string sql = $"SELECT COUNT({column}) from {table}";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return await connection.ExecuteScalarAsync<int>(sql);
        }
    }

    public static async Task<Dictionary<string, int>> GetRecordingMids()
    {
        string sql =
            "SELECT musicbrainz_recording_gid::text, id from music where musicbrainz_recording_gid is not null";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return (await connection.QueryAsync<(string, int)>(sql)).ToDictionary(x => x.Item1, x => x.Item2);
        }
    }

    public static async Task<int> SetSubmittedBy(string url, string submittedBy)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sql =
                "UPDATE music_external_link SET submitted_by = @submitted_by WHERE url = @url";

            int rows = await connection.ExecuteAsync(sql, new { submitted_by = submittedBy, url = url });
            return rows;
        }
    }

    public static async Task<int> DeleteMusicExternalLink(int mId, string url)
    {
        const string sqlDelete = "DELETE from music_external_link where music_id = @music_id AND url = @url";
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());

        int rows = await connection.ExecuteAsync(sqlDelete, new { music_id = mId, url = url });
        if (rows > 0)
        {
            await EvictFromSongsCache(mId);
        }

        return rows;
    }

    public static async Task<List<Song>> GetSongsByTitleAndArtistFuzzy(List<string> titles, List<string> artists,
        SongSourceSongType[] songSourceSongTypes)
    {
        var ret = new List<Song>();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            await connection.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            HashSet<int> aIds = (await FindArtistIdsByArtistNames(artists)).Select(x => x.Item1).ToHashSet();
            if (!aIds.Any())
            {
                return ret;
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
            queryMusic.Where($"(mt.latin_title % ANY({titles}) OR mt.non_latin_title % ANY({titles}))");
            queryMusic.Where($"a.id = ANY({aIds.ToArray()})");
            queryMusic.Where($"msm.type = ANY({songSourceSongTypes.Cast<int>().ToArray()})");

            // Console.WriteLine(queryMusic.Sql);
            // Console.WriteLine(JsonSerializer.Serialize(queryMusic.Parameters, Utils.JsoIndented));

            var mids = (await queryMusic.QueryAsync<int>()).ToList();
            ret.AddRange(await SelectSongsMIds(mids.ToArray(), false));
        }

        return ret;
    }

    public static async Task<List<MusicExternalLink>> FindMusicExternalLinkBySha256(string sha256)
    {
        const string sql = "SELECT * from music_external_link where sha256 = @sha256";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return (await connection.QueryAsync<MusicExternalLink>(sql, new { sha256 = sha256 })).ToList();
        }
    }

    public static async Task<List<ReviewQueue>> FindReviewQueueBySha256(string sha256)
    {
        const string sql = "SELECT * from review_queue where sha256 = @sha256";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return (await connection.QueryAsync<ReviewQueue>(sql, new { sha256 = sha256 })).ToList();
        }
    }

    public static async Task<T?> GetEntity<T>(int id) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return await connection.GetAsync<T?>(id);
        }
    }

    public static async Task<bool> UpdateEntity<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return await connection.UpdateAsync(entity);
        }
    }

    public static async Task<long> InsertEntity<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            bool _ = await connection.InsertAsync(entity);
            return entity.GetIdentityValue();
        }
    }

    public static async Task<bool> InsertEntityBulk<T>(IEnumerable<T> entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return await connection.InsertListAsync(entity);
        }
    }

    public static async Task<bool> UpsertEntity<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return await connection.UpsertAsync(entity);
        }
    }

    public static async Task<bool> DeleteEntity<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            return await connection.DeleteAsync(entity);
        }
    }

    public static async Task<string> GetRandomScreenshotUrl(SongSource songSource, ScreenshotKind screenshotKind)
    {
        string ret = "";
        string sourceVndbId = songSource.Links.First(x => x.Type == SongSourceLinkType.VNDB).Url.ToVndbId();
        switch (screenshotKind)
        {
            case ScreenshotKind.None:
                break;
            case ScreenshotKind.VN:
                {
                    const string sql = "SELECT scr from vn_screenshots where id = @id";
                    await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb()))
                    {
                        string? screenshot = (await connection.QueryAsync<string?>(sql, new { id = sourceVndbId }))
                            .Shuffle().FirstOrDefault();
                        if (!string.IsNullOrEmpty(screenshot))
                        {
                            (string modStr, int number) = Utils.ParseVndbScreenshotStr(screenshot);
                            ret = $"https://emqselfhost/selfhoststorage/vndb-img/sf/{modStr}/{number}.jpg"
                                .ReplaceSelfhostLink();
                        }
                    }

                    break;
                }
            case ScreenshotKind.VNCover:
                {
                    const string sql = "SELECT image from vn where id = @id";
                    await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb()))
                    {
                        string? screenshot = (await connection.QueryAsync<string?>(sql, new { id = sourceVndbId }))
                            .Shuffle().FirstOrDefault();
                        if (!string.IsNullOrEmpty(screenshot))
                        {
                            (string modStr, int number) = Utils.ParseVndbScreenshotStr(screenshot);
                            ret = $"https://emqselfhost/selfhoststorage/vndb-img/cv/{modStr}/{number}.jpg"
                                .ReplaceSelfhostLink();
                        }
                    }

                    break;
                }
            case ScreenshotKind.Character:
                {
                    const string sql =
                        "SELECT c.image from chars c join chars_vns cv on cv.id = c.id join vn v on v.id = cv.vid where c.image is not null and v.id = @id";
                    await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb()))
                    {
                        string? screenshot = (await connection.QueryAsync<string?>(sql, new { id = sourceVndbId }))
                            .Shuffle().FirstOrDefault();
                        if (!string.IsNullOrEmpty(screenshot))
                        {
                            (string modStr, int number) = Utils.ParseVndbScreenshotStr(screenshot);
                            ret = $"https://emqselfhost/selfhoststorage/vndb-img/ch/{modStr}/{number}.jpg"
                                .ReplaceSelfhostLink();
                        }
                    }

                    break;
                }
            case ScreenshotKind.VNPreferExplicit:
                {
                    const string sql =
                        "SELECT scr from vn_screenshots vs join images i on i.id = vs.scr where vs.id = @id and i.c_sexual_avg > 100";
                    await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb()))
                    {
                        string? screenshot = (await connection.QueryAsync<string?>(sql, new { id = sourceVndbId }))
                            .Shuffle().FirstOrDefault();
                        if (!string.IsNullOrEmpty(screenshot))
                        {
                            (string modStr, int number) = Utils.ParseVndbScreenshotStr(screenshot);
                            ret = $"https://emqselfhost/selfhoststorage/vndb-img/sf/{modStr}/{number}.jpg"
                                .ReplaceSelfhostLink();
                        }
                        else
                        {
                            ret = await GetRandomScreenshotUrl(songSource, ScreenshotKind.VN);
                        }
                    }

                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(screenshotKind), screenshotKind, null);
        }

        return ret;
    }

    public static async Task<bool> OverwriteMusic(int oldMid, Song newSong, bool isImport,
        NpgsqlTransaction? transaction = null)
    {
        if (newSong.MusicBrainzRecordingGid != null)
        {
            // need to update musicbrainz_recording_gid in music and maybe the musicbrainz_release_recording table at least
            throw new NotImplementedException();
        }

        NpgsqlConnection? connection = transaction?.Connection;
        bool ownConnection = false;
        if (connection is null)
        {
            ownConnection = true;
            connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            await connection.OpenAsync();
            transaction = await connection.BeginTransactionAsync();
        }

        // todo cleanup of music_source and artist?
        // todo this deletes non_latin_title if isImport
        var oldSong = (await SelectSongsMIds(new[] { oldMid }, false)).Single();
        int rowsDeletedMt = await connection.ExecuteAsync("DELETE FROM music_title where music_id = @mId",
            new { mId = oldMid }, transaction);
        if (rowsDeletedMt <= 0)
        {
            throw new Exception("Failed to delete mt");
        }

        int rowsDeletedMsm = await connection.ExecuteAsync("DELETE FROM music_source_music where music_id = @mId",
            new { mId = oldMid }, transaction);
        if (rowsDeletedMsm <= 0)
        {
            throw new Exception("Failed to delete msm");
        }

        // keep C/A/L when doing vndb or musicbrainz imports
        string roleClause = " AND (role = 0 OR role = 1)";
        int rowsDeletedA = await connection.ExecuteAsync(
            "DELETE FROM artist_music where music_id = @mId" + (isImport ? roleClause : ""),
            new { mId = oldMid }, transaction);
        if (rowsDeletedA <= 0)
        {
            throw new Exception("Failed to delete a");
        }

        int rowsDeletedMel = 0;
        if (!isImport) // newSong won't contain Links if isImport
        {
            rowsDeletedMel = await connection.ExecuteAsync("DELETE FROM music_external_link where music_id = @mId",
                new { mId = oldMid }, transaction);
        }

        newSong.Id = oldMid;
        int mId = await InsertSong(newSong, connection, transaction, !isImport);
        if (mId <= 0 || mId != oldMid)
        {
            throw new Exception($"Failed to insert song: {newSong}");
        }

        if (rowsDeletedMel > 0)
        {
            Console.WriteLine($"rowsDeletedMel: {rowsDeletedMel}");
            // extra check to make sure we don't lose any file links
            var insertedSong = (await SelectSongsMIds(new[] { mId }, false, transaction)).Single();
            var o = JsonSerializer.SerializeToNode(oldSong.Links.Where(x => x.IsFileLink).OrderBy(x => x.Url));
            var n = JsonSerializer.SerializeToNode(insertedSong.Links.Where(x => x.IsFileLink).OrderBy(x => x.Url));
            if (!JsonNode.DeepEquals(o, n))
            {
                throw new Exception(
                    $"Links differ: {JsonSerializer.Serialize(o)} versus {JsonSerializer.Serialize(n)}");
            }
        }

        if (ownConnection)
        {
            await transaction!.CommitAsync();
            await connection.DisposeAsync();
            await transaction.DisposeAsync();
        }

        return true;
    }

    public static async Task<bool> OverwriteArtist(int oldAid, SongArtist newArtist, bool isImport,
        NpgsqlTransaction? transaction = null)
    {
        NpgsqlConnection? connection = transaction?.Connection;
        bool ownConnection = false;
        if (connection is null)
        {
            ownConnection = true;
            connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            await connection.OpenAsync();
            transaction = await connection.BeginTransactionAsync();
        }

        // var oldArtist = (await SelectArtistBatchNoAM(connection,
        //     new List<Song> { new() { Artists = new List<SongArtist> { new() { Id = oldAid } } } }, false)).Single();

        int rowsDeletedAel = await connection.ExecuteAsync("DELETE FROM artist_external_link where artist_id = @aId",
            new { aId = oldAid }, transaction);
        if (rowsDeletedAel <= 0)
        {
            throw new Exception("Failed to delete ael");
        }

        int _ = await connection.ExecuteAsync(
            "DELETE FROM artist_artist where source = @aId or target = @aId",
            new { aId = oldAid }, transaction);

        newArtist.Id = oldAid;
        (int aId, List<int> aaIds) = await InsertArtist(newArtist, transaction!);
        if ((aId <= 0 || aId != oldAid) || aaIds.Any(x => x <= 0))
        {
            throw new Exception($"Failed to insert artist: {newArtist}");
        }

        if (ownConnection)
        {
            await transaction!.CommitAsync();
            await connection.DisposeAsync();
            await transaction.DisposeAsync();
        }

        return true;
    }

    public static async Task<List<SHRoomContainer>> GetSHRoomContainers(int userId,
        DateTime startDate, DateTime endDate)
    {
        var shRoomContainers = new List<SHRoomContainer>();
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

        var queryQuizIds = connection.QueryBuilder(
            $"SELECT DISTINCT quiz_id FROM quiz_song_history where user_id = {userId} AND (played_at >= {startDate} AND played_at <= {endDate})");

        var quizIds = (await connection.QueryAsync<Guid>(queryQuizIds.Sql, queryQuizIds.Parameters)).ToArray();
        var quizzes = (await connection.QueryAsync<EntityQuiz>(
                "SELECT * FROM quiz where id = ANY(@quizIds) order by created_at desc", new { quizIds = quizIds }))
            .ToArray();

        var roomIds = quizzes.Select(x => x.room_id).ToArray();
        var rooms = (await connection.QueryAsync<EntityRoom>(
                "SELECT * FROM room where id = ANY(@roomIds) order by created_at desc", new { roomIds = roomIds }))
            .ToArray();

        var quizSongHistories = (await connection.QueryAsync<QuizSongHistory>(
            $"SELECT * FROM quiz_song_history where quiz_id = ANY(@quizIds) order by played_at", // not desc here!
            new { quizIds = quizIds })).ToArray();

        var usernamesDict = (await connectionAuth.QueryAsync<(int, string)>("select id, username from users"))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

        foreach (EntityRoom room in rooms)
        {
            var shQuizContainers = new List<SHQuizContainer>();
            var roomQuizzes = quizzes.Where(x => x.room_id == room.id);
            foreach (EntityQuiz roomQuiz in roomQuizzes)
            {
                var songHistories = new Dictionary<int, SongHistory>();
                var qsh = quizSongHistories.Where(y => y.quiz_id == roomQuiz.id).GroupBy(z => z.sp);
                foreach (IGrouping<int, QuizSongHistory> histories in qsh)
                {
                    var firstHistory = histories.First();
                    var sh = new SongHistory
                    {
                        MId = firstHistory.music_id,
                        PlayedAt = firstHistory.played_at,
                        PlayerGuessInfos = histories.ToDictionary(c => c.user_id, c => new GuessInfo
                        {
                            Username = Utils.UserIdToUsername(usernamesDict, c.user_id),
                            Guess = c.guess,
                            FirstGuessMs = c.first_guess_ms,
                            IsGuessCorrect = c.is_correct,
                            Labels = null,
                            IsOnList = c.is_on_list,
                        }),
                    };

                    songHistories[histories.Key] = sh;
                }

                shQuizContainers.Add(new SHQuizContainer { Quiz = roomQuiz, SongHistories = songHistories, });
            }

            shRoomContainers.Add(new SHRoomContainer { Room = room, Quizzes = shQuizContainers, });
        }

        int[] mIds = shRoomContainers.SelectMany(x =>
            x.Quizzes.SelectMany(y => y.SongHistories.Select(z => z.Value.MId))).ToArray();

        if (mIds.Any())
        {
            var songs = (await SelectSongsMIds(mIds, false)).ToDictionary(x => x.Id, x => x);
            foreach (SHRoomContainer shRoomContainer in shRoomContainers)
            {
                foreach (SHQuizContainer shQuizContainer in shRoomContainer.Quizzes)
                {
                    foreach ((int _, SongHistory? value) in shQuizContainer.SongHistories)
                    {
                        var song = songs[value.MId].Clone();
                        song.PlayedAt = value.PlayedAt;
                        value.Song = song;
                    }
                }
            }
        }

        return shRoomContainers;
    }

    public static async Task<UserSpacedRepetition?> GetPreviousSpacedRepetitionInfo(int userId, int musicId)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var userSpacedRepetition = await connection.QuerySingleOrDefaultAsync<UserSpacedRepetition?>(
            "SELECT * FROM user_spaced_repetition where user_id = @userId AND music_id = @musicId",
            new { userId, musicId });

        return userSpacedRepetition;
    }

    public static async Task<List<int>> GetMidsWithReviewsDue(List<int> userIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var mids = await connection.QueryAsync<int>(
            "SELECT music_id from user_spaced_repetition where user_id = ANY(@userIds) and due_at < now()",
            new { userIds });
        return mids.ToList();
    }

    public static async Task<List<int>> GetMidsWithIntervals(List<int> userIds)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var mids = await connection.QueryAsync<int>(
            "SELECT music_id from user_spaced_repetition where user_id = ANY(@userIds)",
            new { userIds });
        return mids.ToList();
    }

    public static async Task<ResGetPublicUserInfo?> GetPublicUserInfo(int userId)
    {
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var user = await connectionAuth.QuerySingleOrDefaultAsync<User>(
            "SELECT username, roles, created_at, avatar, skin from users where id = @userId", new { userId });

        if (user is null)
        {
            return null;
        }

        const string sql =
            @"select count(music_id) as count, (100 / (count(music_id)::real / COALESCE(NULLIF(count(is_correct) filter(where is_correct), 0), 1))) as gr from quiz_song_history
where user_id = @userId
group by user_id
";

        const string sqlSSST = @"
WITH counts AS (
    SELECT type, COUNT(*) AS total, SUM(CASE WHEN is_correct THEN 1 ELSE 0 END) AS correct
    FROM quiz_song_history qsh
    JOIN music_source_music msm ON qsh.music_id = msm.music_id
    WHERE user_id = @userId
    GROUP BY TYPE
),
percentages AS (
    SELECT type, total, correct, ROUND(100.0 * correct / total, 2) AS percentage
    FROM counts
)
SELECT type, total, correct, percentage
FROM percentages
ORDER BY type";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        (int count, float gr) = await connection.QuerySingleOrDefaultAsync<(int count, float gr)>(sql, new { userId });

        Dictionary<SongSourceSongType, GetPublicUserInfoSSST> getPublicUserInfoSSST =
            (await connection.QueryAsync<GetPublicUserInfoSSST>(sqlSSST, new { userId }))
            .ToDictionary(x => x.Type, x => x);

        var res = new ResGetPublicUserInfo
        {
            UserId = userId,
            SongCount = count,
            GuessRate = (float)Math.Round(gr, 2),
            Username = user.username,
            Avatar = new Avatar(user.avatar, user.skin),
            UserRoleKind = (UserRoleKind)user.roles,
            CreatedAt = user.created_at,
            SSST = getPublicUserInfoSSST,
        };

        return res;
    }

    public static async Task<ILookup<int, PlayerSongStats>> GetSHPlayerSongStats(List<int> mIds, List<int> userIds)
    {
        const string sql =
            @"select sq.user_id as userid, sq.music_id AS musicid, count(sq.is_correct) filter(where sq.is_correct) as timescorrect, count(sq.music_id) as timesplayed, count(sq.guessed) as timesguessed, sum(sq.first_guess_ms) as totalguessms
from (
select qsh.music_id, qsh.user_id, qsh.is_correct, qsh.first_guess_ms, NULLIF(qsh.guess, '') as guessed
from quiz q
join quiz_song_history qsh on qsh.quiz_id = q.id
where q.should_update_stats and music_id = ANY(@mIds) and user_id = ANY(@userIds)
order by qsh.played_at desc
) sq
group by sq.user_id, sq.music_id
";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var ret = (await connection.QueryAsync<PlayerSongStats>(sql, new { mIds, userIds })).ToLookup(x => x.UserId);

        return ret;
    }

    public static async Task<SHSongStats[]> GetSHSongStats(int mId)
    {
        string sql =
            @$"select *
from (
select qsh.music_id as MusicId, qsh.user_id as UserId, qsh.is_correct as IsCorrect, qsh.first_guess_ms as FirstGuessMs, NULLIF(qsh.guess, '') as Guess, qsh.played_at as PlayedAt,
       row_number() over (partition by qsh.user_id order by qsh.played_at desc) as RowNumber
from quiz q
join quiz_song_history qsh on qsh.quiz_id = q.id
where q.should_update_stats and music_id = @mId
order by qsh.played_at desc
) sq
where RowNumber <= {Constants.SHUseLastNPlaysPerPlayer}
limit 777;
";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var ret = (await connection.QueryAsync<SHSongStats>(sql, new { mId })).ToArray();

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var usernamesDict = (await connectionAuth.QueryAsync<(int, string)>("select id, username from users"))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

        foreach (SHSongStats shSongStats in ret)
        {
            shSongStats.Username = Utils.UserIdToUsername(usernamesDict, shSongStats.UserId);
        }

        return ret;
    }

    public static async Task<ResGetPublicUserInfoSongs?> GetPublicUserInfoSongs(int userId)
    {
        const string sqlMostPlayedSongs =
            @"select qsh.music_id AS MusicId, count(*) as Played, count(qsh.is_correct) FILTER (WHERE qsh.is_correct) AS Correct, usr.interval_days AS IntervalDays
from quiz q
join quiz_song_history qsh on qsh.quiz_id = q.id
LEFT JOIN user_spaced_repetition usr ON usr.music_id = qsh.music_id AND usr.user_id = qsh.user_id
where q.should_update_stats and qsh.user_id = @userId
GROUP BY qsh.music_id, usr.interval_days
ORDER BY count(*) DESC
limit 50;
";

        const string sqlCommonPlayers =
            @"WITH cte AS (
SELECT DISTINCT qsh.quiz_id
FROM quiz_song_history qsh
WHERE qsh.user_id = @userId
AND EXISTS (SELECT 1 FROM quiz_song_history qsh2 WHERE qsh2.quiz_id = qsh.quiz_id AND qsh2.user_id != @userId)
)
SELECT array_agg(DISTINCT user_id) AS arr, qsh.quiz_id AS id
FROM quiz_song_history qsh
JOIN cte ON cte.quiz_id = qsh.quiz_id
GROUP BY qsh.quiz_id
";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var resMostPlayedSongs =
            (await connection.QueryAsync<ResMostPlayedSongs>(sqlMostPlayedSongs, new { userId })).ToArray();

        var musicVotesVocals = await GetUserMusicVotes(userId, SongSourceSongTypeMode.Vocals);
        var musicVotesBgm = await GetUserMusicVotes(userId, SongSourceSongTypeMode.BGM);
        var resUserMusicVotesVocals = musicVotesVocals
            .Select(x => new ResUserMusicVotes { MusicVote = x, IsBGM = false }).ToArray();
        var resUserMusicVotesBgm = musicVotesBgm
            .Select(x => new ResUserMusicVotes { MusicVote = x, IsBGM = true }).ToArray();

        if (!resMostPlayedSongs.Any() && !resUserMusicVotesVocals.Any() && !resUserMusicVotesBgm.Any())
        {
            return null;
        }

        var songs =
            (await SelectSongsMIdsCached(
                resMostPlayedSongs.Select(x => x.MusicId)
                    .Concat(musicVotesVocals.Concat(musicVotesBgm).Select(x => x.music_id)).Distinct()
                    .ToArray()))
            .ToDictionary(x => x.Id, x => x);
        foreach ((_, Song value) in songs)
        {
            value.Links = value.Links.Where(x => x.IsFileLink).ToList();
            foreach (SongLink link in value.Links)
            {
                link.AnalysisRaw = null;
                link.Sha256 = "";
                link.SubmittedBy = null;
            }
        }

        // todo only convert to songmini once?
        foreach (ResMostPlayedSongs resMostPlayedSong in resMostPlayedSongs)
        {
            var song = songs[resMostPlayedSong.MusicId];
            resMostPlayedSong.SongMini = new SongMini
            {
                Id = song.Id,
                S = song.ToStringLatin(),
                L = song.Links,
                A = Converters.GetSingleTitle(song.Artists.First().Titles).LatinTitle
            };
        }

        foreach (ResUserMusicVotes resUserMusicVote in resUserMusicVotesVocals.Concat(resUserMusicVotesBgm))
        {
            var song = songs[resUserMusicVote.MusicVote.music_id];
            resUserMusicVote.SongMini = new SongMini
            {
                Id = song.Id,
                S = song.ToStringLatin(),
                L = song.Links,
                A = Converters.GetSingleTitle(song.Artists.First().Titles).LatinTitle
            };
        }

        var resCommonPlayers =
            (await connection.QueryAsync<(int[] arr, Guid quizId)>(sqlCommonPlayers, new { userId })).ToArray();

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var usernamesDict = (await connectionAuth.QueryAsync<(int, string)>("select id, username from users"))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

        Dictionary<int, int> commonPlayersDict = new();
        foreach ((int[] arr, Guid _) in resCommonPlayers)
        {
            foreach (int id in arr)
            {
                if (id == userId)
                {
                    continue;
                }

                if (!commonPlayersDict.ContainsKey(id))
                {
                    commonPlayersDict[id] = 0;
                }

                commonPlayersDict[id] += 1;
            }
        }

        var commonPlayers = commonPlayersDict.OrderByDescending(x => x.Value).Select(x => new ResCommonPlayers
        {
            UserLite = new UserLite { Id = x.Key, Username = Utils.UserIdToUsername(usernamesDict, x.Key) },
            QuizCount = x.Value
        }).ToArray();

        var res = new ResGetPublicUserInfoSongs
        {
            MostPlayedSongs = resMostPlayedSongs,
            CommonPlayers = commonPlayers,
            UserMusicVotes = resUserMusicVotesVocals.Concat(resUserMusicVotesBgm).ToArray(),
        };

        return res;
    }

    public static async Task<List<UserStat>> GetUserStats()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

        var u = await connectionAuth.QueryAsync<(int userId, string username, DateTime createdAt)>(
            "select id as userId, username, created_at as createdAt from users order by created_at desc");

        var qsh = (await connection.QueryAsync<(int userId, int count)>(
                "select user_id as userId, count(*) as count from quiz_song_history group by user_id"))
            .ToDictionary(x => x.userId, x => x.count);

        var mv = (await connection.QueryAsync<(int userId, int count)>(
                "select user_id as userId, count(*) as count from music_vote where not user_id = any(@ign) group by user_id",
                new { ign = IgnoredMusicVotes }))
            .ToDictionary(x => x.userId, x => x.count);

        var res = new List<UserStat>();
        foreach ((int userId, string username, DateTime createdAt) in u)
        {
            _ = qsh.TryGetValue(userId, out int q);
            _ = mv.TryGetValue(userId, out int m);
            res.Add(new UserStat
            {
                Id = userId,
                Username = username,
                CreatedAt = createdAt,
                Played = q,
                AvgPlaysPerDay = (float)Math.Round(q / (double)((int)(DateTime.UtcNow - createdAt).TotalDays + 1), 2),
                Votes = m,
            });
        }

        return res;
    }

    public static async Task<LabelStats> GetLabelStats(int[] mIds)
    {
        const string sqlAvg = @"
SELECT ((1.0 * sum(stat_correct) / COALESCE(NULLIF(sum(stat_played), 0), 1)) * 100) AS CorrectPercentage,
sum(stat_totalguessms) / COALESCE(NULLIF(sum(stat_guessed), 0), 1) AS GuessMs,
avg(stat_uniqueusers) AS UniqueUsers
FROM music
WHERE id = ANY(@mIds)
and stat_played > 0
";

        const string sqlMs = @"
SELECT count(DISTINCT music_source_id) FROM music_source_music
where music_id = ANY(@mIds)
";

        const string sqlA = @"
SELECT count(DISTINCT artist_id) FROM artist_music
where music_id = ANY(@mIds)
";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var retAvg = await connection.QuerySingleAsync<LabelStats>(sqlAvg, new { mIds });
        retAvg.TotalSongs = mIds.Length;
        retAvg.TotalSources = await connection.QuerySingleAsync<int>(sqlMs, new { mIds });
        retAvg.TotalArtists = await connection.QuerySingleAsync<int>(sqlA, new { mIds });
        retAvg.SongDifficultyLevels = await GetSongDifficultyLevelCounts(mIds);

        return retAvg;
    }

    public static async Task EvictFromSongsCache(int musicId)
    {
        while (CachedSongs.ContainsKey(musicId))
        {
            CachedSongs.TryRemove(musicId, out _);
        }

        // todo enable after setting up live MusicBrainz imports
        bool b = false;
        if (b)
        {
            await RefreshMusicIdsRecordingGidsCache();
        }
    }

    public static async Task<string> GetCharacterImageId(string cId)
    {
        const string sql = "SELECT c.image from chars c where c.image is not null and c.id = @cId";
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb());
        string? screenshot = await connection.QuerySingleOrDefaultAsync<string?>(sql, new { cId });
        return screenshot ?? "";
    }

    public static async Task<ServerActivityStats> GetServerActivityStats(DateTime startDate, DateTime endDate)
    {
        string sqlDailyPlayers = $@"
SELECT to_char(played_at, 'yyyy-mm-dd'), count(DISTINCT user_id) FILTER(WHERE user_id < {Constants.PlayerIdGuestMin}) AS users, count(DISTINCT user_id) FILTER(WHERE user_id >= {Constants.PlayerIdGuestMin}) AS guests
FROM quiz_song_history qsh
WHERE played_at >= @startDate AND played_at <= @endDate
--AND user_id < @maxUserId
GROUP BY to_char(played_at, 'yyyy-mm-dd')
";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var resDailyPlayers =
            (await connection.QueryAsync<(string date, int users, int guests)>(sqlDailyPlayers,
                new { startDate, endDate }))
            .ToDictionary(x => x.date,
                x => new ServerActivityStatsDailyPlayers() { Users = x.users, Guests = x.guests });

        var ret = new ServerActivityStats { DailyPlayers = resDailyPlayers };
        return ret;
    }

    public static async Task<MusicVote[]> GetUserMusicVotes(int userId, SongSourceSongTypeMode ssstm)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        return (await connection.QueryAsync<MusicVote>(@"select DISTINCT ON (mv.music_id) mv.* from music_vote mv
JOIN music_source_music msm ON msm.music_id = mv.music_id
where user_id = @userId AND msm.type = ANY(@msmType)",
            new { userId, msmType = ssstm.ToSongSourceSongTypes().Cast<int>().ToArray() })).ToArray();
    }

    public static async Task<ResGetMusicVotes> GetMusicVotes(int musicId)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var musicVotes = (await connection.QueryAsync<MusicVote>(
            "select * from music_vote where music_id = @musicId and not user_id = any(@ign)",
            new { musicId, ign = IgnoredMusicVotes })).ToArray();

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var usernamesDict =
            (await connectionAuth.QueryAsync<(int, string)>(
                "select id, username from users where id = ANY(@userIds)",
                new { userIds = musicVotes.Select(x => x.user_id).ToArray() }))
            .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

        return new ResGetMusicVotes { UsernamesDict = usernamesDict, MusicVotes = musicVotes };
    }

    public static async Task<ResGetSongSource> GetSongSource(SongSource req, Session? session)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var songSource = await SelectSongSourceBatch(connection,
            new List<Song> { new() { Sources = new List<SongSource> { new() { Id = req.Id } } } },
            req.Categories.Any());

        // Console.WriteLine(JsonSerializer.Serialize(songSource, Utils.JsoIndented));
        if (session != null)
        {
            // todo
        }

        var res = new ResGetSongSource() { SongSource = songSource.First().Value.Single(x => x.Key == req.Id).Value, };
        return res;
    }

    public static async Task<ResGetSongArtist> GetSongArtist(SongArtist songArtist, Session? session)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var artist = await SelectArtistBatchNoAM(connection,
            new List<Song> { new() { Artists = new List<SongArtist> { songArtist } } }, false);

        // Console.WriteLine(JsonSerializer.Serialize(songSource, Utils.JsoIndented));
        PlayerSongStats[] playerSongStats = Array.Empty<PlayerSongStats>();
        if (artist.Any() && true) // todo require extra param to fetch this
        {
            if (session != null)
            {
                // todo fetch player-specific information
            }

            // todo should_update_stats filter
            int artistId = artist.First().Value.First().Key; // todo?
            string sqlA =
                $@"SELECT
    qsh.user_id AS UserId,
    COUNT(*) AS TimesPlayed,
    SUM(CASE WHEN qsh.is_correct THEN 1 ELSE 0 END) AS TimesCorrect,
    COALESCE(ROUND(votes.AvgVote, 2), 0) AS VoteAverage,
    COALESCE(votes.VoteCount, 0) AS VoteCount
FROM
    quiz_song_history qsh
JOIN
    artist_music am ON am.music_id = qsh.music_id
LEFT JOIN (
    SELECT
        mv.user_id,
        AVG(mv.vote) / 10 AS AvgVote,
        COUNT(DISTINCT mv.music_id) AS VoteCount
    FROM
        music_vote mv
    JOIN
        artist_music am ON am.music_id = mv.music_id
    WHERE
        am.artist_id = @artistId and mv.user_id < {Constants.PlayerIdGuestMin}
    GROUP BY
        mv.user_id
) AS votes ON votes.user_id = qsh.user_id
WHERE
    am.artist_id = @artistId and qsh.user_id < {Constants.PlayerIdGuestMin}
GROUP BY
    qsh.user_id, votes.AvgVote, votes.VoteCount
ORDER BY
    TimesPlayed DESC;
";
            playerSongStats = (await connection.QueryAsync<PlayerSongStats>(sqlA, new { artistId })).ToArray();

            await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
            var usernamesDict =
                (await connectionAuth.QueryAsync<(int, string)>(
                    "select id, username from users where id = ANY(@userIds)",
                    new { userIds = playerSongStats.Select(x => x.UserId).ToArray() }))
                .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

            foreach (PlayerSongStats playerSongStat in playerSongStats)
            {
                playerSongStat.Username = Utils.UserIdToUsername(usernamesDict, playerSongStat.UserId);
            }
        }

        var res = new ResGetSongArtist
        {
            SongArtists = artist.SelectMany(x => x.Value.Values).ToList(), // todo important distinct
            PlayerSongStats = playerSongStats,
        };
        return res;
    }

    public static async Task<int> SelectNextVal(string seq)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        return await connection.ExecuteScalarAsync<int>("SELECT nextval(@seq)", new { seq });
    }
}
