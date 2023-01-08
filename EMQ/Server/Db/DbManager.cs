using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Dapper;
using Dapper.Contrib.Extensions;
using DapperQueryBuilder;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Core;
using Npgsql;

namespace EMQ.Server.Db;

public static class DbManager
{
    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Titles.LatinTitle <br/>
    /// Song.Links.Url <br/>
    /// </summary>
    public static async Task<List<Song>> SelectSongs(Song input)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var queryMusic = connection
                .QueryBuilder($@"SELECT *
            FROM music m
            LEFT JOIN music_title mt ON mt.music_id = m.id
            LEFT JOIN music_external_link mel ON mel.music_id = m.id
            /**where**/
    ");

            if (input.Id > 0)
            {
                queryMusic.Where($"m.id = {input.Id}");
            }

            var latinTitles = input.Titles.Select(x => x.LatinTitle).ToList();
            if (latinTitles.Any())
            {
                queryMusic.Where($"mt.latin_title = ANY({latinTitles})");
            }

            var links = input.Links.Select(x => x.Url).ToList();
            if (links.Any())
            {
                queryMusic.Where($"mel.url = ANY({links})");
            }

            if (queryMusic.GetFilters() is null)
            {
                throw new Exception("At least one filter must be applied");
            }

            // Console.WriteLine(queryMusic.Sql);

            var songs = new List<Song>();

            await connection.QueryAsync(queryMusic.Sql,
                new[] { typeof(Music), typeof(MusicTitle), typeof(MusicExternalLink), }, (objects) =>
                {
                    // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                    var music = (Music)objects[0];
                    var musicTitle = (MusicTitle)objects[1];
                    var musicExternalLink = (MusicExternalLink?)objects[2];

                    var existingSong = songs.Where(x => x.Id == music.id).ToList().SingleOrDefault();
                    if (existingSong is null)
                    {
                        var song = new Song();
                        var songTitles = new List<Title>();
                        var songLinks = new List<SongLink>();

                        song.Id = music.id;
                        song.Type = (SongType)music.type;

                        if (music.length.HasValue)
                        {
                            song.Length = music.length.Value;
                        }

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
                                Url = musicExternalLink.url,
                                IsVideo = musicExternalLink.is_video,
                                Type = (SongLinkType)musicExternalLink.type
                            });
                        }

                        song.Titles = songTitles.DistinctBy(x => x.LatinTitle).ToList();
                        song.Links = songLinks.DistinctBy(x => x.Url).ToList();

                        songs.Add(song);
                    }
                    else
                    {
                        if (!existingSong.Titles.Any(x => x.LatinTitle == musicTitle.latin_title))
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
                                    Url = musicExternalLink.url,
                                    Type = (SongLinkType)musicExternalLink.type,
                                    IsVideo = musicExternalLink.is_video,
                                });
                            }
                        }
                    }

                    return 0;
                },
                splitOn:
                "music_id,music_id", param: queryMusic.Parameters);


            foreach (Song song in songs)
            {
                song.Sources = await SelectSongSource(connection, song);
                song.Artists = await SelectArtist(connection, song, false);
            }

            // Console.WriteLine("songs: " + JsonSerializer.Serialize(songs, Utils.JsoIndented));

            return songs;
        }
    }

    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Sources.Links <br/>
    /// Song.Sources.Titles.LatinTitle <br/>
    /// </summary>
    public static async Task<List<SongSource>> SelectSongSource(IDbConnection connection, Song input)
    {
        var songSources = new List<SongSource>();
        // var songSourceTitles = new List<SongSourceTitle>();
        // var songSourceLinks = new List<SongSourceLink>();
        // var songSourceCategories = new List<SongSourceCategory>();

        var queryMusicSource = connection
            .QueryBuilder($@"SELECT *
            FROM music_source_music msm
            LEFT JOIN music_source ms ON ms.id = msm.music_source_id
            LEFT JOIN music_source_title mst ON mst.music_source_id = ms.id
            LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
            LEFT JOIN music_source_category msc ON msc.music_source_id = ms.id
            LEFT JOIN category c ON c.id = msc.category_id
            /**where**/
    ");

        if (input.Id > 0)
        {
            queryMusicSource.Where($"msm.music_id = {input.Id}");
        }

        var latinTitles = input.Sources.SelectMany(x => x.Titles.Select(y => y.LatinTitle)).ToList();
        if (latinTitles.Any())
        {
            queryMusicSource.Where($"mst.latin_title = ANY({latinTitles})");
        }

        var links = input.Sources.SelectMany(x => x.Links.Select(y => y.Url)).ToList();
        if (links.Any())
        {
            queryMusicSource.Where($"msel.url = ANY({links})");
        }

        if (queryMusicSource.GetFilters() is null)
        {
            throw new Exception("At least one filter must be applied");
        }

        // Console.WriteLine(queryMusicSource.Sql);

        await connection.QueryAsync(queryMusicSource.Sql,
            new[]
            {
                typeof(MusicSourceMusic), typeof(MusicSource), typeof(MusicSourceTitle),
                typeof(MusicSourceExternalLink), typeof(MusicSourceCategory), typeof(Category)
            }, (objects) =>
            {
                // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                var musicSourceMusic = (MusicSourceMusic)objects[0];
                var musicSource = (MusicSource)objects[1];
                var musicSourceTitle = (MusicSourceTitle)objects[2];
                var musicSourceExternalLink = (MusicSourceExternalLink?)objects[3];
                var category = (Category?)objects[5];

                var existingSongSource = songSources.Where(x => x.Id == musicSource.id).ToList().SingleOrDefault();
                if (existingSongSource is null)
                {
                    songSources.Add(new SongSource()
                    {
                        Id = musicSource.id,
                        Type = (SongSourceType)musicSource.type,
                        AirDateStart = musicSource.air_date_start,
                        AirDateEnd = musicSource.air_date_end,
                        LanguageOriginal = musicSource.language_original,
                        RatingAverage = musicSource.rating_average,
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
                        MusicIds = new HashSet<int>() { musicSourceMusic.music_id }
                    });

                    if (musicSourceExternalLink is not null)
                    {
                        songSources.Last().Links.Add(new SongSourceLink()
                        {
                            Url = musicSourceExternalLink.url,
                            Type = (SongSourceLinkType)musicSourceExternalLink.type
                        });
                    }

                    if (category is not null)
                    {
                        songSources.Last().Categories.Add(new SongSourceCategory()
                        {
                            Name = category.name, Type = (SongSourceCategoryType)category.type
                        });
                    }
                }
                else
                {
                    if (!existingSongSource.Titles.Any(x => x.LatinTitle == musicSourceTitle.latin_title))
                    {
                        existingSongSource.Titles.Add(new Title()
                        {
                            LatinTitle = musicSourceTitle.latin_title,
                            NonLatinTitle = musicSourceTitle.non_latin_title,
                            Language = musicSourceTitle.language,
                            IsMainTitle = musicSourceTitle.is_main_title,
                        });
                    }

                    if (category is not null)
                    {
                        if (!existingSongSource.Categories.Any(x => x.Name == category.name))
                        {
                            existingSongSource.Categories.Add(new SongSourceCategory()
                            {
                                Name = category.name, Type = (SongSourceCategoryType)category.type
                            });
                        }
                    }

                    var songSourceSongType = (SongSourceSongType)musicSourceMusic.type;
                    if (!existingSongSource.SongTypes.Contains(songSourceSongType))
                    {
                        existingSongSource.SongTypes.Add(songSourceSongType);
                    }

                    existingSongSource.MusicIds.Add(musicSourceMusic.music_id);
                }

                return 0;
            },
            splitOn:
            "id,music_source_id,music_source_id,music_source_id,id",
            param: queryMusicSource.Parameters);

        return songSources;
    }

    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Artists.Id <br/>
    /// Song.Artists.Titles.LatinTitle <br/>
    /// </summary>
    public static async Task<List<SongArtist>> SelectArtist(IDbConnection connection, Song input, bool needsRequery)
    {
        var songArtists = new List<SongArtist>();
        // var songArtistAliases = new List<SongArtistAlias>();

        var queryArtist = connection
            .QueryBuilder($@"SELECT *
            FROM artist_music am
            LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
            LEFT JOIN artist a ON a.id = aa.artist_id
            /**where**/
    ");

        if (input.Id > 0)
        {
            queryArtist.Where($"am.music_id = {input.Id}");
        }

        int? artistId = input.Artists.FirstOrDefault()?.Id;
        if (artistId is > 0)
        {
            queryArtist.Where($"a.id = {artistId}");
        }

        var latinTitles = input.Artists.SelectMany(x => x.Titles.Select(y => y.LatinTitle)).ToList();
        if (latinTitles.Any())
        {
            queryArtist.Where($"aa.latin_alias = ANY({latinTitles})");
        }

        if (queryArtist.GetFilters() is null)
        {
            throw new Exception("At least one filter must be applied");
        }


        // Console.WriteLine(queryArtist.Sql);

        await connection.QueryAsync(queryArtist.Sql,
            new[] { typeof(ArtistMusic), typeof(ArtistAlias), typeof(Artist), }, (objects) =>
            {
                // Console.WriteLine(JsonSerializer.Serialize(objects, Utils.Jso));
                var artistMusic = (ArtistMusic)objects[0];
                var artistAlias = (ArtistAlias)objects[1];
                var artist = (Artist)objects[2];

                var existingArtist = songArtists.Where(x => x.Id == artist.id).ToList().SingleOrDefault();
                if (existingArtist is null)
                {
                    var songArtist = new SongArtist()
                    {
                        Id = artist.id,
                        PrimaryLanguage = artist.primary_language,
                        VndbId = artist.vndb_id,
                        Titles = new List<Title>()
                        {
                            new Title()
                            {
                                LatinTitle = artistAlias.latin_alias,
                                NonLatinTitle = artistAlias.non_latin_alias,
                                IsMainTitle = artistAlias.is_main_name
                            },
                        },
                        MusicIds = new HashSet<int> { artistMusic.music_id }
                    };

                    if (artist.sex.HasValue)
                    {
                        songArtist.Sex = (Sex)artist.sex.Value;
                    }

                    songArtist.Role = (SongArtistRole)artistMusic.role;

                    songArtists.Add(songArtist);
                }
                else
                {
                    if (!existingArtist.Titles.Any(x => x.LatinTitle == artistAlias.latin_alias))
                    {
                        existingArtist.Titles.Add(new Title()
                        {
                            LatinTitle = artistAlias.latin_alias, NonLatinTitle = artistAlias.non_latin_alias,
                        });
                    }

                    existingArtist.MusicIds.Add(artistMusic.music_id);
                }

                return 0;
            },
            splitOn:
            "id,id", param: queryArtist.Parameters);

        if (needsRequery && songArtists.Any())
        {
            var inputWithArtistId =
                new Song { Artists = new List<SongArtist> { new() { Id = songArtists.First().Id } } };
            input.Artists = songArtists = await SelectArtist(connection, inputWithArtistId, false);
        }

        return songArtists;
    }

    public static async Task<int> InsertSong(Song song)
    {
        // Console.WriteLine(JsonSerializer.Serialize(song, Utils.Jso));
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var music = new Music() { type = (int)song.Type };
            if (song.Length > 0)
            {
                music.length = song.Length;
            }

            int mId = await connection.InsertAsync(music);

            foreach (Title songTitle in song.Titles)
            {
                int mtId = await connection.InsertAsync(new MusicTitle()
                {
                    music_id = mId,
                    latin_title = songTitle.LatinTitle,
                    non_latin_title = songTitle.NonLatinTitle,
                    language = songTitle.Language,
                    is_main_title = songTitle.IsMainTitle
                });
            }

            foreach (SongLink songLink in song.Links)
            {
                int melId = await connection.InsertAsync(new MusicExternalLink()
                {
                    music_id = mId, url = songLink.Url, type = (int)songLink.Type, is_video = songLink.IsVideo
                });
            }


            foreach (SongSource songSource in song.Sources)
            {
                string msVndbUrl = "";
                try
                {
                    msVndbUrl = song.Sources.Select(x =>
                        x.Links.Select(y => y.Url).Single(z => z.Contains("vndb"))).Single();
                }
                catch (Exception)
                {
                    // ignored
                }

                int msId = 0;
                if (!string.IsNullOrEmpty(msVndbUrl))
                {
                    msId = (await connection.QueryAsync<int>(
                        "select ms.id from music_source_external_link msel join music_source ms on ms.id = msel.music_source_id where msel.url=@mselUrl",
                        new { mselUrl = msVndbUrl })).ToList().FirstOrDefault();
                }

                if (msId > 0)
                {
                }
                else
                {
                    msId = await connection.InsertAsync(new MusicSource()
                    {
                        air_date_start = songSource.AirDateStart,
                        air_date_end = songSource.AirDateEnd,
                        language_original = songSource.LanguageOriginal,
                        rating_average = songSource.RatingAverage,
                        type = (int)songSource.Type
                    });

                    foreach (Title songSourceAlias in songSource.Titles)
                    {
                        int mstId = await connection.InsertAsync(new MusicSourceTitle()
                        {
                            music_source_id = msId,
                            latin_title = songSourceAlias.LatinTitle,
                            non_latin_title = songSourceAlias.NonLatinTitle,
                            language = songSourceAlias.Language,
                            is_main_title = songSourceAlias.IsMainTitle
                        });
                    }

                    foreach (SongSourceLink songSourceLink in songSource.Links)
                    {
                        int mselId = await connection.InsertAsync(new MusicSourceExternalLink()
                        {
                            music_source_id = msId, url = songSourceLink.Url, type = (int)songSourceLink.Type
                        });
                    }

                    foreach (SongSourceCategory songSourceCategory in songSource.Categories)
                    {
                        // todo uniq check
                        int cId = await connection.InsertAsync(new Category()
                        {
                            name = songSourceCategory.Name, type = (int)songSourceCategory.Type
                        });

                        int mscId = await connection.InsertAsync(
                            new MusicSourceCategory() { category_id = cId, music_source_id = msId });
                    }
                }

                foreach (var songSourceSongType in songSource.SongTypes)
                {
                    int msmId = 0;
                    msmId = (await connection.QueryAsync<int>(
                        "select music_id from music_source_music msm where msm.music_id=@mId AND msm.music_source_id =@msId AND msm.type=@songSourceSongType",
                        new { mId, msId, songSourceSongType })).ToList().SingleOrDefault();

                    if (msmId > 0)
                    {
                    }
                    else
                    {
                        msmId = await connection.InsertAsync(new MusicSourceMusic()
                        {
                            music_id = mId, music_source_id = msId, type = (int)songSourceSongType
                        });
                    }
                }
            }


            foreach (SongArtist songArtist in song.Artists)
            {
                if (songArtist.Titles.Count > 1)
                {
                    throw new Exception("Artists can only have one artist_alias per song");
                }

                int aId = 0;
                int aaId = 0;

                if (!string.IsNullOrEmpty(songArtist.VndbId))
                {
                    aId = (await connection.QueryAsync<int>(
                        "select a.id from artist a where a.vndb_id=@aVndbId",
                        new { aVndbId = songArtist.VndbId })).ToList().SingleOrDefault();
                }

                if (aId > 0)
                {
                    aaId = (await connection.QueryAsync<int>(
                            "select aa.id,aa.latin_alias from artist_alias aa join artist a on a.id = aa.artist_id where a.vndb_id=@aVndbId AND aa.latin_alias=@latinAlias",
                            new { aVndbId = songArtist.VndbId, latinAlias = songArtist.Titles.First().LatinTitle }))
                        .ToList().SingleOrDefault();
                }
                else
                {
                    aId = await connection.InsertAsync(new Artist()
                    {
                        primary_language = songArtist.PrimaryLanguage,
                        sex = (int)songArtist.Sex,
                        vndb_id = songArtist.VndbId
                    });
                }

                if (aaId < 1)
                {
                    foreach (Title songArtistAlias in songArtist.Titles)
                    {
                        aaId = await connection.InsertAsync(new ArtistAlias()
                        {
                            artist_id = aId,
                            latin_alias = songArtistAlias.LatinTitle,
                            non_latin_alias = songArtistAlias.NonLatinTitle,
                            is_main_name = songArtistAlias.IsMainTitle
                        });
                    }
                }

                if (mId < 1)
                {
                    throw new Exception("mId is invalid");
                }

                if (aaId < 1)
                {
                    throw new Exception("aaId is invalid");
                }

                int amId = await connection.InsertAsync(
                    new ArtistMusic()
                    {
                        music_id = mId, artist_id = aId, artist_alias_id = aaId, role = (int)songArtist.Role
                    });
            }

            await transaction.CommitAsync();
            return mId;
        }
    }

    public static async Task<List<Song>> GetRandomSongs(int numSongs, bool duplicates,
        List<string>? validSources = null)
    {
        string sqlMusicIds = $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id";

        if (validSources != null && validSources.Any())
        {
            sqlMusicIds += $@" WHERE msel.url = ANY(@validSources)";
        }

        var ret = new List<Song>();
        var addedMselUrls = new List<string>();
        var rng = new Random();

        List<(int, string)> ids;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            ids = (await connection.QueryAsync<(int, string)>(sqlMusicIds, new { validSources }))
                .OrderBy(_ => rng.Next()).ToList();
        }

        // Console.WriteLine(JsonSerializer.Serialize(ids.Select(x => x.Item1)));

        foreach ((int mId, string? mselUrl) in ids)
        {
            if (ret.Count >= numSongs)
            {
                break;
            }

            if (!addedMselUrls.Contains(mselUrl) || duplicates)
            {
                var songs = await SelectSongs(new Song { Id = mId });
                if (songs.Any())
                {
                    foreach (Song song in songs)
                    {
                        if (ret.Count >= numSongs)
                        {
                            break;
                        }

                        if (!addedMselUrls.Contains(mselUrl) || duplicates)
                        {
                            song.StartTime = rng.Next(0, Math.Clamp(song.Length - 20, 2, int.MaxValue));
                            ret.Add(song);
                            addedMselUrls.Add(mselUrl);
                        }
                    }
                }
            }
        }

        return ret;
    }

    public static async Task<List<Song>> GetLootedSongs(int numSongs, bool duplicates, List<string> validSources)
    {
        if (!validSources.Any())
        {
            return new List<Song>();
        }

        string sqlMusicIds = $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     WHERE msel.url = ANY(@validSources)";

        var ret = new List<Song>();
        var addedMselUrls = new List<string>();
        var rng = new Random();

        List<(int, string)> ids;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            ids = (await connection.QueryAsync<(int, string)>(sqlMusicIds, new { validSources }))
                .OrderBy(_ => rng.Next()).ToList();
        }

        // Console.WriteLine(JsonSerializer.Serialize(ids.Select(x => x.Item1)));

        foreach ((int mId, string? mselUrl) in ids)
        {
            if (ret.Count >= numSongs)
            {
                break;
            }

            if (!addedMselUrls.Contains(mselUrl) || duplicates)
            {
                var songs = await SelectSongs(new Song { Id = mId });
                if (songs.Any())
                {
                    var song = songs.First();
                    song.StartTime = rng.Next(0, Math.Clamp(song.Length - 20, 2, int.MaxValue));
                    ret.Add(song);
                    addedMselUrls.Add(mselUrl);
                }
            }
        }

        return ret;
    }

    public static async Task<string> SelectAutocomplete()
    {
        const string sqlAutocomplete =
            @"SELECT mst.latin_title, mst.non_latin_title
            FROM music_source_title mst where language IN ('ja','en','tr')
            ";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var res = (await connection.QueryAsync<(string, string?)>(sqlAutocomplete))
                .Select(x => new[] { x.Item1, x.Item2 }).SelectMany(x => x);
            string autocomplete =
                JsonSerializer.Serialize(res.Distinct().Where(x => x != null).OrderBy(x => x), Utils.Jso);
            return autocomplete;
        }
    }

    public static async Task<IEnumerable<Song>> FindSongsBySongSourceTitle(string songSourceTitle)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songSources = await SelectSongSource(connection,
                new Song
                {
                    Sources = new List<SongSource>
                    {
                        new() { Titles = new List<Title> { new() { LatinTitle = songSourceTitle } } }
                    }
                });

            // Console.WriteLine(JsonSerializer.Serialize(songSources, Utils.JsoIndented));

            foreach (SongSource songSource in songSources)
            {
                foreach (int songSourceMusicId in songSource.MusicIds)
                {
                    songs.AddRange(await SelectSongs(new Song { Id = songSourceMusicId }));
                }
            }
        }

        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsByArtistTitle(string artistTitle)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songArtists = await SelectArtist(connection,
                new Song
                {
                    Artists = new List<SongArtist>
                    {
                        new() { Titles = new List<Title> { new() { LatinTitle = artistTitle } } }
                    }
                }, true);

            // Console.WriteLine(JsonSerializer.Serialize(songArtists, Utils.JsoIndented));

            foreach (var songArtist in songArtists)
            {
                foreach (int songArtistMusicId in songArtist.MusicIds)
                {
                    songs.AddRange(await SelectSongs(new Song { Id = songArtistMusicId }));
                }
            }
        }

        return songs;
    }

    public static async Task<int> InsertSongLink(int mId, SongLink songLink)
    {
        int melId;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var mel = new MusicExternalLink
            {
                music_id = mId, url = songLink.Url, type = (int)songLink.Type, is_video = songLink.IsVideo,
            };

            Console.WriteLine($"Attempting to insert MusicExternalLink: " + JsonSerializer.Serialize(mel, Utils.Jso));
            melId = await connection.InsertAsync(mel);
            if (melId > 0)
            {
                // todo
            }
        }

        return melId;
    }

    public static async Task<int> InsertReviewQueue(int mId, SongLink songLink, string submittedBy)
    {
        int rqId;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var rq = new ReviewQueue()
            {
                music_id = mId,
                url = songLink.Url,
                type = (int)songLink.Type,
                is_video = songLink.IsVideo,
                submitted_by = submittedBy,
                submitted_on = DateTime.UtcNow,
                status = (int)ReviewQueueStatus.Pending,
                reason = null
            };

            rqId = await connection.InsertAsync(rq);
            if (rqId > 0)
            {
                Console.WriteLine($"Inserted ReviewQueue: " + JsonSerializer.Serialize(rq, Utils.Jso));
            }
        }

        return rqId;
    }

    public static async Task<string> ExportSong()
    {
        var songs = await GetRandomSongs(int.MaxValue, true);
        return JsonSerializer.Serialize(songs, Utils.JsoIndented);
    }

    public static async Task<string> ExportSongLite()
    {
        var songs = await GetRandomSongs(int.MaxValue, true);

        var songLite = songs.Select(song => new SongLite
        {
            Titles = song.Titles,
            Links = song.Links,
            SourceVndbIds = song.Sources.SelectMany(songSource =>
                songSource.Links.Where(songSourceLink => songSourceLink.Type == SongSourceLinkType.VNDB)
                    .Select(songSourceLink => songSourceLink.Url.ToVndbId())).ToList(),
            ArtistVndbIds = song.Artists.Select(artist => artist.VndbId ?? "").ToList(),
        }).ToList();

        foreach (SongLite sl in songLite)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sl));
            byte[] hash = MD5.Create().ComputeHash(bytes);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            if (songLite.Any(x => x.Md5Hash == encoded))
            {
                throw new Exception("Duplicate SongLite detected");
            }

            sl.Md5Hash = encoded;
        }

        return JsonSerializer.Serialize(songLite, Utils.JsoIndented);
    }

    public static async Task<string> ExportReviewQueue()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var reviewQueue = (await connection.GetAllAsync<ReviewQueue>()).ToList();
            return JsonSerializer.Serialize(reviewQueue, Utils.JsoIndented);
        }
    }

    public static async Task ImportSongLite(List<SongLite> songLites)
    {
        const string sqlMIdFromSongLite = @"
            SELECT DISTINCT m.id
            FROM music m
            LEFT JOIN music_title mt ON mt.music_id = m.id
            LEFT JOIN music_external_link mel ON mel.music_id = m.id
            LEFT JOIN music_source_music msm ON msm.music_id = m.id
            LEFT JOIN music_source ms ON ms.id = msm.music_source_id
            LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
            LEFT JOIN artist_music am ON am.music_id = m.id
            LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
            LEFT JOIN artist a ON a.id = aa.artist_id
            WHERE mt.latin_title = ANY(@mtLatinTitle)
              AND msel.url = ANY(@mselUrl)
              AND a.vndb_id = ANY(@aVndbId)";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            foreach (SongLite songLite in songLites)
            {
                // Console.WriteLine(JsonSerializer.Serialize(songLite, Utils.JsoIndented));
                // Console.WriteLine(JsonSerializer.Serialize(songLite.Titles.Select(x => x.LatinTitle).ToList(),
                //     Utils.JsoIndented));
                // Console.WriteLine(JsonSerializer.Serialize(
                //     songLite.SourceVndbIds.Select(x => "https://vndb.org/" + x).ToList(), Utils.JsoIndented));
                // Console.WriteLine(JsonSerializer.Serialize(songLite.ArtistVndbIds, Utils.JsoIndented));
                List<int> mIds = (await connection.QueryAsync<int>(sqlMIdFromSongLite,
                        new
                        {
                            mtLatinTitle = songLite.Titles.Select(x => x.LatinTitle).ToList(),
                            mselUrl = songLite.SourceVndbIds.Select(x => x.ToVndbUrl()).ToList(),
                            aVndbId = songLite.ArtistVndbIds
                        }))
                    .ToList();

                if (!mIds.Any())
                {
                    throw new Exception(
                        $"No matches for {JsonSerializer.Serialize(songLite, Utils.JsoIndented)}");
                }

                if (mIds.Count > 1)
                {
                    throw new Exception(
                        $"Multiple matches for {JsonSerializer.Serialize(songLite, Utils.JsoIndented)}");
                }

                foreach (int mId in mIds)
                {
                    foreach (SongLink link in songLite.Links)
                    {
                        await InsertSongLink(mId, link);
                    }
                }
            }

            await transaction.CommitAsync();
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

    public static async Task<IEnumerable<RQ>> FindRQs(DateTime startDate, DateTime endDate)
    {
        var rqs = new List<RQ>();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            // todo date filter
            var reviewQueues = (await connection.GetAllAsync<ReviewQueue>()).ToList();
            foreach (ReviewQueue reviewQueue in reviewQueues)
            {
                var rq = new RQ
                {
                    id = reviewQueue.id,
                    music_id = reviewQueue.music_id,
                    url = reviewQueue.url,
                    type = (SongLinkType)reviewQueue.type,
                    is_video = reviewQueue.is_video,
                    submitted_by = reviewQueue.submitted_by,
                    submitted_on = reviewQueue.submitted_on,
                    status = (ReviewQueueStatus)reviewQueue.status,
                    reason = reviewQueue.reason,
                    Song = (await SelectSongs(new Song { Id = reviewQueue.music_id })).Single()
                };
                rqs.Add(rq);
            }
        }

        return rqs;
    }

    public static async Task<int> UpdateReviewQueueItem(int rqId, ReviewQueueStatus requestedStatus)
    {
        int melId = -1;
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
                    throw new NotImplementedException($"Cannot update approved item {rqId}.");
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (requestedStatus)
            {
                case ReviewQueueStatus.Pending:
                case ReviewQueueStatus.Rejected:
                    break;
                case ReviewQueueStatus.Approved:
                    var songLink = new SongLink() { Url = rq.url, Type = (SongLinkType)rq.type, IsVideo = rq.is_video };
                    melId = await InsertSongLink(rq.music_id, songLink);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedStatus), requestedStatus, null);
            }

            rq.status = (int)requestedStatus;
            await connection.UpdateAsync(rq);
            Console.WriteLine($"Updated ReviewQueue: " + JsonSerializer.Serialize(rq, Utils.Jso));
        }

        return melId;
    }
}
