using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorApp1.Server.Db.Entities;
using BlazorApp1.Shared.Quiz.Entities.Concrete;
using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;

namespace BlazorApp1.Server.Db;

public static class DbManager
{
    public static async Task<Song> SelectSong(int mId)
    {
        const string sqlMusic =
            @"SELECT *
            FROM music m
            LEFT JOIN music_title mt ON mt.music_id = m.id
            LEFT JOIN music_external_link mel ON mel.music_id = m.id
            where m.id = @mId
            ";

        const string sqlMusicSource =
            @"SELECT *
            FROM music_source_music msm
            LEFT JOIN music_source ms ON ms.id = msm.music_source_id
            LEFT JOIN music_source_title mst ON mst.music_source_id = ms.id
            LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
            LEFT JOIN music_source_category msc ON msc.music_source_id = ms.id
            LEFT JOIN category c ON c.id = msc.category_id
            where msm.music_id = @mId
            ";

        const string sqlArtist =
            @"SELECT *
            FROM artist_music am
            LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
            LEFT JOIN artist a ON a.id = aa.artist_id
            where am.music_id = @mId
            ";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            var songs = await connection.QueryAsync(sqlMusic,
                new[] { typeof(Music), typeof(MusicTitle), typeof(MusicExternalLink), }, (objects) =>
                {
                    // Console.WriteLine(JsonSerializer.Serialize(objects));
                    var song = new Song() // todo
                    {
                        Id = ((Music)objects[0]).id,
                        Titles = new List<SongTitle>()
                        {
                            new SongTitle() { LatinTitle = ((MusicTitle)objects[1]).latin_title, }
                        },
                        Links = new List<SongLink> { new() { Url = ((MusicExternalLink)objects[2]).url } },
                    };

                    return song;
                },
                splitOn:
                "music_id,music_id", param: new { mId });
            var song = songs.ToList().Single();

            var songSources = await connection.QueryAsync(sqlMusicSource,
                new[]
                {
                    typeof(MusicSourceMusic), typeof(MusicSource), typeof(MusicSourceTitle),
                    typeof(MusicSourceExternalLink), typeof(MusicSourceCategory), typeof(Category)
                }, (objects) =>
                {
                    Console.WriteLine(JsonSerializer.Serialize(objects) + Environment.NewLine);
                    var songSource = new SongSource() // todo
                    {
                        Aliases = new List<string> { ((MusicSourceTitle)objects[2]).latin_title },
                        Categories = new List<SongSourceCategory>()
                        {
                            new SongSourceCategory()
                            {
                                Name = ((Category)objects[5]).name, Type = ((Category)objects[5]).type
                            }
                        }
                    };

                    return songSource;
                },
                splitOn:
                "id,music_source_id,music_source_id,music_source_id,id",
                param: new { mId });
            song.Sources = songSources.ToList();

            var songArtists = await connection.QueryAsync(sqlArtist,
                new[] { typeof(ArtistMusic), typeof(ArtistAlias), typeof(Artist), }, (objects) =>
                {
                    // Console.WriteLine(JsonSerializer.Serialize(objects));
                    var songSource = new SongArtist() // todo
                    {
                        Aliases = new List<string>() { ((ArtistAlias)objects[1]).latin_alias }
                    };

                    return songSource;
                },
                splitOn:
                "id,id", param: new { mId });
            song.Artists = songArtists.ToList();

            Console.WriteLine("song: " + JsonSerializer.Serialize(song));
            return song;
        }
    }

    public static async Task InsertSong(Song song)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            int mId = await connection.InsertAsync(new Music() { type = 0 }); // todo

            foreach (SongTitle songTitle in song.Titles)
            {
                int mtId = await connection.InsertAsync(new MusicTitle()
                {
                    music_id = mId,
                    latin_title = songTitle.LatinTitle,
                    non_latin_title = songTitle.NonLatinTitle,
                    language = songTitle.Language,
                    is_main_title = true
                });
            }

            foreach (SongLink songLink in song.Links)
            {
                int melId = await connection.InsertAsync(new MusicExternalLink()
                {
                    music_id = mId, url = songLink.Url, type = 0, is_video = songLink.IsVideo
                }); // todo
            }


            foreach (SongSource songSource in song.Sources)
            {
                int msId = await connection.InsertAsync(new MusicSource()
                {
                    air_date_start = DateTime.Now, language_original = 0, type = 0
                }); // todo

                foreach (string songSourceAlias in songSource.Aliases)
                {
                    int mstId = await connection.InsertAsync(new MusicSourceTitle()
                    {
                        music_source_id = msId, latin_title = songSourceAlias, language = 0
                    }); // todo
                }

                int mselId =
                    await connection.InsertAsync(new MusicSourceExternalLink()
                    {
                        music_source_id = msId, url = "vndb", type = 0
                    }); // todo

                int msmId = await connection.InsertAsync(new MusicSourceMusic()
                {
                    music_id = mId, music_source_id = msId, type = 0
                }); // todo

                foreach (SongSourceCategory songSourceCategory in songSource.Categories)
                {
                    // todo uniq check
                    int cId = await connection.InsertAsync(new Category()
                    {
                        name = songSourceCategory.Name, type = songSourceCategory.Type
                    });

                    int mscId = await connection.InsertAsync(
                        new MusicSourceCategory() { category_id = cId, music_source_id = msId });
                }
            }


            foreach (SongArtist songArtist in song.Artists)
            {
                int aId = await connection.InsertAsync(new Artist() { }); // todo

                foreach (string songArtistAlias in songArtist.Aliases)
                {
                    int aaId = await connection.InsertAsync(new ArtistAlias()
                    {
                        artist_id = aId, latin_alias = songArtistAlias, is_main_name = true
                    }); // todo

                    int amId = await connection.InsertAsync(
                        new ArtistMusic() { music_id = mId, artist_alias_id = aaId }); // todo
                }
            }

            await transaction.CommitAsync();
        }
    }

    public static async Task<List<Song>> GetRandomSongs(int numSongs)
    {
        var songs = new List<Song>();
        var ids = new HashSet<int>();
        var rand = new Random();
        int numSongsInDb = 10; // todo

        while (ids.Count < numSongs)
        {
            ids.Add(rand.Next(4, numSongsInDb));
        }

        Console.WriteLine(JsonSerializer.Serialize(ids));
        foreach (int id in ids)
        {
            songs.Add(await SelectSong(id));
        }

        return songs;
    }
}
