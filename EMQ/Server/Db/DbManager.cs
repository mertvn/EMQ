using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Dapper;
using Dapper.Contrib.Extensions;
using DapperQueryBuilder;
using EMQ.Server.Business;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz;
using Npgsql;

namespace EMQ.Server.Db;

public static class DbManager
{
    private static Dictionary<int, Song> CachedSongs { get; } = new();

    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Titles.LatinTitle <br/>
    /// Song.Links.Url <br/>
    /// </summary>
    public static async Task<List<Song>> SelectSongs(Song input)
    {
        var latinTitles = input.Titles.Select(x => x.LatinTitle).ToList();
        var links = input.Links.Select(x => x.Url).ToList();

        if (input.Id > 0 && !latinTitles.Any() && !links.Any())
        {
            if (CachedSongs.TryGetValue(input.Id, out var s))
            {
                return new List<Song> { s };
            }
        }

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

                        // if (music.length.HasValue)
                        // {
                        //     song.Length = music.length.Value;
                        // }

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
                                Type = (SongLinkType)musicExternalLink.type,
                                Duration = musicExternalLink.duration,
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
                                    Duration = musicExternalLink.duration,
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

                CachedSongs[input.Id] = song;
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
    /// Song.Sources.Titles.NonLatinTitle <br/>
    /// Song.Sources.Categories.VndbId <br/>
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

        var latinTitles = input.Sources.SelectMany(x => x.Titles.Select(y => y.LatinTitle))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (latinTitles.Any())
        {
            // todo? ILIKE instead of =
            queryMusicSource.Where($"mst.latin_title = ANY({latinTitles})");
        }

        List<string> nonLatinTitles = input.Sources.SelectMany(x => x.Titles.Select(y => y.NonLatinTitle))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList()!;
        if (nonLatinTitles.Any())
        {
            queryMusicSource.Where($"mst.non_latin_title = ANY({nonLatinTitles})");
        }

        var links = input.Sources.SelectMany(x => x.Links.Select(y => y.Url)).ToList();
        if (links.Any())
        {
            queryMusicSource.Where($"msel.url = ANY({links})");
        }

        // todo needs to take type into account as well / or just query with Id instead of VndbId
        var categories = input.Sources.SelectMany(x => x.Categories.Select(y => y.VndbId)).ToList();
        if (categories.Any())
        {
            queryMusicSource.Where($"c.vndb_id = ANY({categories})");
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
                var musicSourceCategory = (MusicSourceCategory?)objects[4];
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
                        RatingBayesian = musicSource.rating_bayesian,
                        Popularity = musicSource.popularity,
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
                            Id = category.id,
                            Name = category.name,
                            VndbId = category.vndb_id,
                            Type = (SongSourceCategoryType)category.type,
                            Rating = musicSourceCategory!.rating,
                        });

                        if (musicSourceCategory.spoiler_level.HasValue)
                        {
                            songSources.Last().Categories.Last().SpoilerLevel =
                                (SpoilerLevel)musicSourceCategory.spoiler_level.Value;
                        }
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
                                Id = category.id,
                                Name = category.name,
                                VndbId = category.vndb_id,
                                Type = (SongSourceCategoryType)category.type,
                                Rating = musicSourceCategory!.rating,
                            });

                            if (musicSourceCategory.spoiler_level.HasValue)
                            {
                                existingSongSource.Categories.Last().SpoilerLevel =
                                    (SpoilerLevel)musicSourceCategory.spoiler_level.Value;
                            }
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

        // todo do this properly when the size increase gets annoying
        // if (!categories.Any())
        // {
        //     foreach (SongSource songSource in songSources)
        //     {
        //         songSource.Categories = new List<SongSourceCategory>();
        //     }
        // }

        return songSources;
    }

    /// <summary>
    /// Available filters: <br/>
    /// Song.Id <br/>
    /// Song.Artists.Id <br/>
    /// Song.Artists.Titles.LatinTitle <br/>
    /// Song.Artists.Titles.NonLatinTitle <br/>
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

        var latinTitles = input.Artists.SelectMany(x => x.Titles.Select(y => y.LatinTitle))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList();
        if (latinTitles.Any())
        {
            queryArtist.Where($"lower(aa.latin_alias) = ANY(lower({latinTitles}::text)::text[])");
        }

        List<string> nonLatinTitles = input.Artists.SelectMany(x => x.Titles.Select(y => y.NonLatinTitle))
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .ToList()!;
        if (nonLatinTitles.Any())
        {
            queryArtist.Where($"aa.non_latin_alias = ANY({nonLatinTitles})");
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
                                IsMainTitle = artistAlias.is_main_name,
                                Language = artist.primary_language ?? "",
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
            songArtists = await SelectArtist(connection, inputWithArtistId, false);
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
            // if (song.Length > 0)
            // {
            //     music.length = song.Length;
            // }

            int mId = await connection.InsertAsync(music);

            foreach (Title songTitle in song.Titles)
            {
                // ReSharper disable once UnusedVariable
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
                // ReSharper disable once UnusedVariable
                int melId = await connection.InsertAsync(new MusicExternalLink()
                {
                    music_id = mId,
                    url = songLink.Url,
                    type = (int)songLink.Type,
                    is_video = songLink.IsVideo,
                    duration = songLink.Duration
                });
            }


            int msId = 0;
            foreach (SongSource songSource in song.Sources)
            {
                string msVndbUrl = songSource.Links.Single(y => y.Type == SongSourceLinkType.VNDB).Url;

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
                        rating_bayesian = songSource.RatingBayesian,
                        popularity = songSource.Popularity,
                        votecount = songSource.VoteCount,
                        type = (int)songSource.Type
                    });

                    foreach (Title songSourceAlias in songSource.Titles)
                    {
                        // ReSharper disable once UnusedVariable
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
                        // ReSharper disable once UnusedVariable
                        int mselId = await connection.InsertAsync(new MusicSourceExternalLink()
                        {
                            music_source_id = msId, url = songSourceLink.Url, type = (int)songSourceLink.Type
                        });
                    }

                    foreach (SongSourceCategory songSourceCategory in songSource.Categories)
                    {
                        int cId = (await connection.QueryAsync<int>(
                            "select id from category c where c.vndb_id=@songSourceCategoryVndbId AND c.type=@songSourceCategoryType",
                            new
                            {
                                songSourceCategoryVndbId = songSourceCategory.VndbId,
                                songSourceCategoryType = songSourceCategory.Type
                            })).ToList().SingleOrDefault();

                        if (cId > 0)
                        {
                        }
                        else
                        {
                            var newCategory = new Category()
                            {
                                name = songSourceCategory.Name,
                                type = (int)songSourceCategory.Type,
                                vndb_id = songSourceCategory.VndbId,
                            };

                            cId = await connection.InsertAsync(newCategory);
                        }

                        int mscId = (await connection.QueryAsync<int>(
                            "select music_source_id from music_source_category msc where msc.music_source_id=@msId AND msc.category_id =@cId",
                            new { msId, cId })).ToList().SingleOrDefault();

                        if (mscId > 0)
                        {
                        }
                        else
                        {
                            // ReSharper disable once RedundantAssignment
                            mscId = await connection.InsertAsync(
                                new MusicSourceCategory()
                                {
                                    category_id = cId,
                                    music_source_id = msId,
                                    rating = songSourceCategory.Rating,
                                    spoiler_level = (int?)songSourceCategory.SpoilerLevel,
                                });
                        }
                    }
                }

                foreach (var songSourceSongType in songSource.SongTypes)
                {
                    int msmId = (await connection.QueryAsync<int>(
                        "select music_id from music_source_music msm where msm.music_id=@mId AND msm.music_source_id =@msId AND msm.type=@songSourceSongType",
                        new { mId, msId, songSourceSongType })).ToList().SingleOrDefault();

                    if (msmId > 0)
                    {
                    }
                    else
                    {
                        // ReSharper disable once RedundantAssignment
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

                if (msId < 1)
                {
                    throw new Exception("msId is invalid");
                }

                if (aaId < 1)
                {
                    throw new Exception("aaId is invalid");
                }

                // ReSharper disable once UnusedVariable
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

    // todo make Filters required
    public static async Task<List<Song>> GetRandomSongs(int numSongs, bool duplicates,
        List<string>? validSources = null, QuizFilters? filters = null, bool printSql = false,
        bool keepCategories = false)
    {
        // 1. Find all valid music ids
        var ret = new List<Song>();
        var rng = Random.Shared;

        List<(int, string)> ids;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            string sqlMusicIds;
            if (filters != null && filters.CategoryFilters.Any())
            {
                sqlMusicIds =
                    $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     JOIN music_source_category msc on msc.music_source_id = ms.id
                                     JOIN category c on c.id = msc.category_id
                                     WHERE 1=1
                                     ";
            }
            else
            {
                sqlMusicIds =
                    $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     WHERE 1=1
                                     ";
            }

            var queryMusicIds = connection.QueryBuilder($"{sqlMusicIds:raw}");

            // Apply filters
            if (filters != null)
            {
                var validCategories = filters.CategoryFilters;
                if (validCategories.Any())
                {
                    var trileans = validCategories.Select(x => x.Trilean);
                    bool hasInclude = trileans.Any(y => y is LabelKind.Include);

                    var ordered = validCategories.OrderByDescending(x => x.Trilean == LabelKind.Include)
                        .ThenByDescending(y => y.Trilean == LabelKind.Maybe)
                        .ThenByDescending(z => z.Trilean == LabelKind.Exclude).ToList();
                    for (int index = 0; index < ordered.Count; index++)
                    {
                        CategoryFilter categoryFilter = ordered[index];
                        // Console.WriteLine("processing c " + categoryFilter.SongSourceCategory.VndbId);

                        if (index > 0)
                        {
                            switch (categoryFilter.Trilean)
                            {
                                case LabelKind.Maybe:
                                    if (hasInclude)
                                    {
                                        continue;
                                    }

                                    queryMusicIds.AppendLine($"UNION");
                                    break;
                                case LabelKind.Include:
                                    queryMusicIds.AppendLine($"INTERSECT");
                                    break;
                                case LabelKind.Exclude:
                                    queryMusicIds.AppendLine($"EXCEPT");
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            queryMusicIds.Append($"{sqlMusicIds:raw}");
                        }

                        // todo vndb_id null
                        queryMusicIds.Append(
                            $@" AND c.vndb_id = {categoryFilter.SongSourceCategory.VndbId}
 AND msc.spoiler_level <= {(int?)categoryFilter.SongSourceCategory.SpoilerLevel ?? int.MaxValue}
 AND msc.rating >= {categoryFilter.SongSourceCategory.Rating ?? 0}");
                    }
                }

                var validSongSourceSongTypes = filters.SongSourceSongTypeFilters.Where(x => x.Value).ToList();
                if (validSongSourceSongTypes.Any())
                {
                    queryMusicIds.Append($"\n");
                    for (int index = 0; index < validSongSourceSongTypes.Count; index++)
                    {
                        (SongSourceSongType songSourceSongType, _) = validSongSourceSongTypes.ElementAt(index);
                        queryMusicIds.Append(index == 0
                            ? (FormattableString)$" AND ( msm.type = {(int)songSourceSongType}"
                            : (FormattableString)$" OR msm.type = {(int)songSourceSongType}");
                    }

                    queryMusicIds.Append($")");
                }

                if (filters.StartDateFilter != DateTime.Parse(Constants.QFDateMin) ||
                    filters.EndDateFilter != DateTime.Parse(Constants.QFDateMax))
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

                if (filters.PopularityStart != Constants.QFPopularityMin ||
                    filters.PopularityEnd != Constants.QFPopularityMax)
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append($"ms.popularity >= {filters.PopularityStart}");
                    queryMusicIds.Append($" AND ms.popularity <= {filters.PopularityEnd}");
                    queryMusicIds.Append($")");
                }

                if (filters.VoteCountStart != Constants.QFVoteCountMin ||
                    filters.VoteCountEnd != Constants.QFVoteCountMax)
                {
                    queryMusicIds.Append($"\n");
                    queryMusicIds.Append($" AND (");
                    queryMusicIds.Append($"ms.votecount >= {filters.VoteCountStart}");
                    queryMusicIds.Append($" AND ms.votecount <= {filters.VoteCountEnd}");
                    queryMusicIds.Append($")");
                }
            }

            if (validSources != null && validSources.Any())
            {
                queryMusicIds.Append($@" AND msel.url = ANY({validSources})");
            }

            if (printSql)
            {
                Console.WriteLine(queryMusicIds.Sql);
                Console.WriteLine(JsonSerializer.Serialize(queryMusicIds.Parameters, Utils.JsoIndented));
            }

            ids = (await connection.QueryAsync<(int, string)>(queryMusicIds.Sql, queryMusicIds.Parameters))
                .OrderBy(_ => rng.Next()).ToList();
            // Console.WriteLine(JsonSerializer.Serialize(ids.Select(x => x.Item1)));
        }

        // 2. Select Song objects with those music ids until we hit the desired NumSongs, respecting the Duplicates setting
        var addedMselUrls = new List<string>();
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
                            song.StartTime = rng.Next(0,
                                Math.Clamp((int)SongLink.GetShortestLink(song.Links).Duration.TotalSeconds - 40, 2,
                                    int.MaxValue));
                            ret.Add(song);
                            addedMselUrls.Add(mselUrl);
                        }
                    }
                }
            }
        }

        if (!keepCategories)
        {
            // todo
            foreach (SongSource songSource in ret.SelectMany(song => song.Sources))
            {
                songSource.Categories = new List<SongSourceCategory>();
            }
        }

        return ret;
    }

    // todo merge with the other method
    public static async Task<List<Song>> GetLootedSongs(int numSongs, bool duplicates, List<string> validSources)
    {
        if (!validSources.Any())
        {
            return new List<Song>();
        }

        string sqlMusicIds =
            $@"SELECT DISTINCT ON (mel.music_id) mel.music_id, msel.url FROM music_external_link mel
                                     JOIN music m on m.id = mel.music_id
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     WHERE msel.url = ANY(@validSources)";

        var ret = new List<Song>();
        var addedMselUrls = new List<string>();
        var rng = Random.Shared;

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
                    song.StartTime = rng.Next(0,
                        Math.Clamp((int)SongLink.GetShortestLink(song.Links).Duration.TotalSeconds - 40, 2,
                            int.MaxValue));
                    ret.Add(song);
                    addedMselUrls.Add(mselUrl);
                }
            }
        }

        return ret;
    }

    public static async Task<string> SelectAutocompleteMst()
    {
        const string sqlAutocompleteMst =
            @"SELECT mst.latin_title, mst.non_latin_title
            FROM music_source_title mst where language IN ('ja','en','tr')
            "; // #blamerampaa

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var res = (await connection.QueryAsync<(string, string?)>(sqlAutocompleteMst))
                .Select(x => new[] { x.Item1, x.Item2 }).SelectMany(x => x);
            string autocomplete =
                JsonSerializer.Serialize(res.Distinct().Where(x => x != null).OrderBy(x => x), Utils.Jso);
            return autocomplete;
        }
    }

    public static async Task<string> SelectAutocompleteC()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var categories = await connection.GetAllAsync<Category>();
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
            @"SELECT a.id, aa.latin_alias, aa.non_latin_alias
            FROM artist_music am
            LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
            LEFT JOIN artist a ON a.id = aa.artist_id
            ";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var res = (await connection.QueryAsync<(int, string, string?)>(sqlAutocompleteA))
                .Select(x => new AutocompleteA(x.Item1, x.Item2, x.Item3 ?? ""));
            string autocomplete =
                JsonSerializer.Serialize(res.DistinctBy(x => x), Utils.Jso);
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

            if (!songSources.Any())
            {
                songSources = await SelectSongSource(connection,
                    new Song
                    {
                        Sources = new List<SongSource>
                        {
                            new() { Titles = new List<Title> { new() { NonLatinTitle = songSourceTitle } } }
                        }
                    });
            }

            // Console.WriteLine(JsonSerializer.Serialize(songSources, Utils.JsoIndented));

            foreach (SongSource songSource in songSources)
            {
                foreach (int songSourceMusicId in songSource.MusicIds)
                {
                    songs.AddRange(await SelectSongs(new Song { Id = songSourceMusicId }));
                }
            }
        }

        // todo
        foreach (SongSource songSource in songs.SelectMany(song => song.Sources))
        {
            songSource.Categories = new List<SongSourceCategory>();
        }

        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsBySongSourceCategories(
        List<SongSourceCategory> songSourceCategories)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songSources = await SelectSongSource(connection,
                new Song { Sources = new List<SongSource> { new() { Categories = songSourceCategories } } });

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

        // todo
        foreach (SongSource songSource in songs.SelectMany(song => song.Sources))
        {
            songSource.Categories = new List<SongSourceCategory>();
        }

        return songs;
    }

    public static async Task<IEnumerable<Song>> FindSongsByArtistId(int artistId)
    {
        List<Song> songs = new();
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songArtists = await SelectArtist(connection,
                new Song { Artists = new List<SongArtist> { new() { Id = artistId } } }, false);

            // Console.WriteLine(JsonSerializer.Serialize(songArtists, Utils.JsoIndented));

            foreach (var songArtist in songArtists)
            {
                foreach (int songArtistMusicId in songArtist.MusicIds)
                {
                    songs.AddRange(await SelectSongs(new Song { Id = songArtistMusicId }));
                }
            }
        }

        // todo
        foreach (SongSource songSource in songs.SelectMany(song => song.Sources))
        {
            songSource.Categories = new List<SongSourceCategory>();
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
                music_id = mId,
                url = songLink.Url,
                type = (int)songLink.Type,
                is_video = songLink.IsVideo,
                duration = songLink.Duration
            };

            if (mel.duration == default)
            {
                throw new Exception("MusicExternalLink duration cannot be 0.");
            }

            Console.WriteLine(
                $"Attempting to insert MusicExternalLink: " + JsonSerializer.Serialize(mel, Utils.Jso));
            melId = await connection.InsertAsync(mel);
            if (melId > 0)
            {
                // todo
            }
        }

        return melId;
    }

    public static async Task<int> InsertReviewQueue(int mId, SongLink songLink, string submittedBy,
        string? analysis = null)
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
                    type = (int)songLink.Type,
                    is_video = songLink.IsVideo,
                    submitted_by = submittedBy,
                    submitted_on = DateTime.UtcNow,
                    status = (int)ReviewQueueStatus.Pending,
                    reason = null,
                };

                if (!string.IsNullOrWhiteSpace(analysis))
                {
                    rq.analysis = analysis;
                }

                rqId = await connection.InsertAsync(rq);
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

    public static async Task<string> ExportSong()
    {
        var songs = await GetRandomSongs(int.MaxValue, true);
        return JsonSerializer.Serialize(songs, Utils.JsoIndented);
    }

    public static async Task<string> ExportSongLite()
    {
        var songs = await GetRandomSongs(int.MaxValue, true);
        var songLite = songs.Select(song => Song.ToSongLite(song)).ToList();

        HashSet<string> md5Hashes = new();
        foreach (SongLite sl in songLite)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sl));
            byte[] hash = MD5.Create().ComputeHash(bytes);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            if (!md5Hashes.Add(encoded))
            {
                throw new Exception("Duplicate SongLite detected");
            }
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
                    analysis = reviewQueue.analysis,
                    Song = (await SelectSongs(new Song { Id = reviewQueue.music_id })).Single(),
                    duration = reviewQueue.duration,
                };
                rqs.Add(rq);
            }
        }

        return rqs.OrderBy(x => x.id);
    }

    public static async Task<int> UpdateReviewQueueItem(int rqId, ReviewQueueStatus requestedStatus,
        string? reason = null, MediaAnalyserResult? analyserResult = null)
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
                    var songLink = new SongLink()
                    {
                        Url = rq.url,
                        Type = (SongLinkType)rq.type,
                        IsVideo = rq.is_video,
                        Duration = rq.duration!.Value
                    };
                    melId = await InsertSongLink(rq.music_id, songLink);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(requestedStatus), requestedStatus, null);
            }

            rq.status = (int)requestedStatus;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                rq.reason = reason;
            }

            if (analyserResult != null)
            {
                string analyserResultStr;
                if (analyserResult.IsValid)
                {
                    analyserResultStr = "OK";
                }
                else
                {
                    analyserResultStr = string.Join(", ", analyserResult.Warnings.Select(x => x.ToString()));
                }

                rq.analysis = analyserResultStr;
                rq.duration = analyserResult.Duration;
            }

            await connection.UpdateAsync(rq);
            Console.WriteLine($"Updated ReviewQueue: " + JsonSerializer.Serialize(rq, Utils.Jso));
        }

        return melId;
    }

    public static async Task<LibraryStats> SelectLibraryStats(int limit = 50)
    {
        // todo external_link vndb type check
        // todo cache results?
        const string sqlMusic =
            "SELECT COUNT(DISTINCT m.id) FROM music m LEFT JOIN music_external_link mel ON mel.music_id = m.id";

        const string sqlMusicSource =
            "SELECT COUNT(DISTINCT ms.id) FROM music_source_music msm LEFT JOIN music_source ms ON ms.id = msm.music_source_id LEFT JOIN music_external_link mel ON mel.music_id = msm.music_id";

        const string sqlArtist =
            "SELECT COUNT(DISTINCT a.id) FROM artist_music am LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id LEFT JOIN artist a ON a.id = aa.artist_id LEFT JOIN music_external_link mel ON mel.music_id = am.music_id";

        const string sqlWhereClause = " WHERE mel.url is not null";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            int totalMusicCount = await connection.QuerySingleAsync<int>(sqlMusic);
            int availableMusicCount = await connection.QuerySingleAsync<int>(sqlMusic + sqlWhereClause);
            int totalMusicSourceCount = await connection.QuerySingleAsync<int>(sqlMusicSource);
            int availableMusicSourceCount = await connection.QuerySingleAsync<int>(sqlMusicSource + sqlWhereClause);
            int totalArtistCount = await connection.QuerySingleAsync<int>(sqlArtist);
            int availableArtistCount = await connection.QuerySingleAsync<int>(sqlArtist + sqlWhereClause);


            string sqlMusicType =
                @"SELECT msm.type as Type, COUNT(DISTINCT m.id) as MusicCount
FROM music m
LEFT JOIN music_external_link mel ON mel.music_id = m.id
LEFT JOIN music_source_music msm ON msm.music_id = m.id
/**where**/
group by msm.type
order by type";
            var qMusicType = connection.QueryBuilder($"{sqlMusicType:raw}");
            var totalMusicTypeCount = (await qMusicType.QueryAsync<LibraryStatsMusicType>()).ToList();
            qMusicType.Where($"mel.url is not null");
            var availableMusicTypeCount = (await qMusicType.QueryAsync<LibraryStatsMusicType>()).ToList();


            var mels = (await connection.QueryAsync<MusicExternalLink>("SELECT * FROM music_external_link")).ToList();
            int videoLinkCount = mels.Count(x => x.is_video && !mels.Any(y => !y.is_video && y.music_id == x.music_id));
            int soundLinkCount = mels.Count(x => !x.is_video && !mels.Any(y => y.is_video && y.music_id == x.music_id));
            int bothLinkCount = mels.Count(x => x.is_video && mels.Any(y => !y.is_video && y.music_id == x.music_id));


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
            var msm = (await qMsm.QueryAsync<LibraryStatsMsm>()).ToList();

            qMsm.Where($"mel.url is not null");
            qMsm.Append($"LIMIT {limit:raw}");
            var msmAvailable = (await qMsm.QueryAsync<LibraryStatsMsm>()).ToList();

            for (int index = 0; index < msmAvailable.Count; index++)
            {
                LibraryStatsMsm msmA = msmAvailable[index];

                msmA.AvailableMusicCount = msmA.MusicCount;
                var match = msm.Where(x => x.MstLatinTitle == msmA.MstLatinTitle);
                msmA.MusicCount = match.Sum(x => x.MusicCount);
            }


            string sqlArtistMusic =
                @"SELECT a.id AS AId, a.vndb_id AS VndbId, COUNT(DISTINCT m.id) AS MusicCount
FROM music m
LEFT JOIN music_external_link mel ON mel.music_id = m.id
LEFT JOIN artist_music am ON am.music_id = m.id
LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
LEFT JOIN artist a ON a.id = aa.artist_id
/**where**/
group by a.id, a.vndb_id ORDER BY COUNT(DISTINCT m.id) desc";
            var qAm = connection.QueryBuilder($"{sqlArtistMusic:raw}");
            var am = (await qAm.QueryAsync<LibraryStatsAm>()).ToList();

            qAm.Where($"mel.url is not null");
            qAm.Append($"LIMIT {limit:raw}");
            var amAvailable = (await qAm.QueryAsync<LibraryStatsAm>()).ToList();

            var artistAliases = (await connection.QueryAsync<(int aId, string aaLatinAlias, bool aaIsMainName)>(
                    "select a.id, aa.latin_alias, aa.is_main_name from artist_alias aa LEFT JOIN artist a ON a.id = aa.artist_id"))
                .ToList();
            foreach (LibraryStatsAm libraryStatsAm in am)
            {
                var aliases = artistAliases.Where(x => x.aId == libraryStatsAm.AId).ToList();
                var mainAlias = aliases.SingleOrDefault(x => x.aaIsMainName);
                libraryStatsAm.AALatinAlias =
                    mainAlias != default ? mainAlias.aaLatinAlias : aliases.First().aaLatinAlias;
            }

            foreach (LibraryStatsAm libraryStatsAm in amAvailable)
            {
                var aliases = artistAliases.Where(x => x.aId == libraryStatsAm.AId).ToList();
                var mainAlias = aliases.SingleOrDefault(x => x.aaIsMainName);
                libraryStatsAm.AALatinAlias =
                    mainAlias != default ? mainAlias.aaLatinAlias : aliases.First().aaLatinAlias;
            }

            for (int index = 0; index < amAvailable.Count; index++)
            {
                LibraryStatsAm amA = amAvailable[index];

                amA.AvailableMusicCount = amA.MusicCount;
                var match = am.Where(x => x.AId == amA.AId);
                amA.MusicCount = match.Sum(x => x.MusicCount);
            }


            string sqlMsYear =
                @"SELECT date_trunc('year', ms.air_date_start) AS year, Count(m.id)
FROM music m
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
LEFT JOIN music_external_link mel ON mel.music_id = m.id
/**where**/
group by year
order by year";
            var qMsYear = connection.QueryBuilder($"{sqlMsYear:raw}");
            var msYear =
                (await qMsYear.QueryAsync<(DateTime, int)>()).ToDictionary(x => x.Item1, x => x.Item2);

            qMsYear.Where($"mel.url is not null");
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


            var libraryStats = new LibraryStats
            {
                TotalMusicCount = totalMusicCount,
                AvailableMusicCount = availableMusicCount,
                TotalMusicSourceCount = totalMusicSourceCount,
                AvailableMusicSourceCount = availableMusicSourceCount,
                TotalArtistCount = totalArtistCount,
                AvailableArtistCount = availableArtistCount,
                TotalLibraryStatsMusicType = totalMusicTypeCount,
                AvailableLibraryStatsMusicType = availableMusicTypeCount,
                VideoLinkCount = videoLinkCount,
                SoundLinkCount = soundLinkCount,
                BothLinkCount = bothLinkCount,
                msm = msm.Take(limit).ToList(),
                msmAvailable = msmAvailable,
                am = am.Take(limit).ToList(),
                amAvailable = amAvailable,
                msYear = msYear,
                msYearAvailable = msYearAvailable,
            };

            return libraryStats;
        }
    }

    public static async Task<List<Song>> FindSongsByLabels(List<Label> reqLabels)
    {
        var validSources = Label.GetValidSourcesFromLabels(reqLabels);
        // return await GetRandomSongs(int.MaxValue, true, validSources); // todo make mel param

        string sqlMusicIdsNoMel =
            $@"SELECT DISTINCT ON (m.id) m.id, msel.url FROM
                                     music m
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id
                                     WHERE msel.url = ANY(@validSources)";

        var ret = new List<Song>();
        var addedMselUrls = new List<string>();
        var rng = Random.Shared;
        bool duplicates = true;
        int numSongs = int.MaxValue;

        List<(int, string)> ids;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            ids = (await connection.QueryAsync<(int, string)>(sqlMusicIdsNoMel, new { validSources }))
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
                    song.StartTime = rng.Next(0,
                        Math.Clamp((int)SongLink.GetShortestLink(song.Links).Duration.TotalSeconds - 40, 2,
                            int.MaxValue));
                    ret.Add(song);
                    addedMselUrls.Add(mselUrl);
                }
            }
        }

        // todo
        foreach (SongSource songSource in ret.SelectMany(song => song.Sources))
        {
            songSource.Categories = new List<SongSourceCategory>();
        }

        return ret.OrderBy(x => x.Id).ToList();
    }

    public static async Task<List<int>> FindArtistIdsByArtistNames(List<string> artistNames)
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

            HashSet<int> aIds = new();
            foreach (string createrName in createrNames)
            {
                var song = new Song
                {
                    Artists = new List<SongArtist>
                    {
                        new() { Titles = new List<Title> { new() { LatinTitle = createrName } } }
                    }
                };
                var artist = (await DbManager.SelectArtist(connection, song, true)).SingleOrDefault();
                if (artist != null)
                {
                    aIds.Add(artist.Id);
                }

                song.Artists.Single().Titles.Single().LatinTitle = "";
                song.Artists.Single().Titles.Single().NonLatinTitle = createrName;
                var artist2 = (await DbManager.SelectArtist(connection, song, true)).SingleOrDefault();
                if (artist2 != null)
                {
                    aIds.Add(artist2.Id);
                }
            }

            return aIds.ToList();
        }
    }

    public static async Task<List<int>> FindMidsWithSoundLinks()
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var mids = (await connection.QueryAsync<int>("SELECT * FROM music_external_link where is_video = false"))
                .ToList();
            return mids;
        }
    }

    public static async Task<IEnumerable<Song>> FindSongsByVndbUrl(string vndbUrl)
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMusicIds =
                $@"SELECT DISTINCT ON (m.id) m.id FROM music m
                                     JOIN music_source_music msm on msm.music_id = m.id
                                     JOIN music_source ms on msm.music_source_id = ms.id
                                     JOIN music_source_external_link msel on ms.id = msel.music_source_id";

            var queryMusicIds = connection.QueryBuilder($"{sqlMusicIds:raw}");
            queryMusicIds.Append($@" AND msel.url = {vndbUrl}");

            var mIds = (await queryMusicIds.QueryAsync<int>()).ToList();

            List<Song> songs = new();
            foreach (int mId in mIds)
            {
                var song = await SelectSongs(new Song() { Id = mId });
                songs.AddRange(song);
            }

            // todo
            foreach (SongSource songSource in songs.SelectMany(song => song.Sources))
            {
                songSource.Categories = new List<SongSourceCategory>();
            }

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
}
