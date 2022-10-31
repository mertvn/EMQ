using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;
using Npgsql;
using NUnit.Framework;

namespace Tests;

public class DbTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test_SelectSong_1()
    {
        var song = await DbManager.SelectSong(1);
        Assert.That(song.Id > 0);

        Assert.That(song.Titles.First().LatinTitle.Any());
        Assert.That(song.Titles.First().Language.Any());

        // Assert.That(song.Links.First().Url.Any());

        Assert.That(song.Sources.First().Id > 0);
        Assert.That(song.Sources.First().Titles.First().LatinTitle.Any());
        Assert.That(song.Sources.First().Titles.First().Language.Any());
        Assert.That(song.Sources.First().Links.First().Url.Any());
        // Assert.That(song.Sources.First().Categories.First().Name.Any());

        Assert.That(song.Artists.First().Id > 0);
        Assert.That(song.Artists.First().Titles.First().LatinTitle.Any());
    }

    [Test]
    public async Task Test_GetRandomSongs_100()
    {
        var songs = await DbManager.GetRandomSongs(100);

        foreach (Song song in songs)
        {
            Assert.That(song.Id > 0);
            Assert.That(song.Titles.First().LatinTitle.Any());
            // Assert.That(song.Links.First().Url.Any());
            Assert.That(song.Sources.First().Titles.First().LatinTitle.Any());
            // Assert.That(song.Sources.First().Categories.First().Name.Any());
            Assert.That(song.Artists.First().Titles.First().LatinTitle.Any());
        }
    }

    [Test]
    public async Task Test_InsertSong()
    {
        var song = new Song()
        {
            Length = 266,
            Titles =
                new List<Title>()
                {
                    new Title() { LatinTitle = "Desire", Language = "en", IsMainTitle = true },
                    new Title() { LatinTitle = "Desire2", Language = "ja" }
                },
            Artists = new List<SongArtist>()
            {
                new SongArtist()
                {
                    Role = SongArtistRole.Vocals,
                    VndbId = "s1440",
                    PrimaryLanguage = "ja",
                    Titles =
                        new List<Title>()
                        {
                            new Title()
                            {
                                LatinTitle = "Misato Aki",
                                Language = "ja",
                                NonLatinTitle = "美郷あき",
                                IsMainTitle = true
                            }
                        },
                    Sex = Sex.Female
                }
            },
            Links =
                new List<SongLink>()
                {
                    new SongLink()
                    {
                        Url = "https://files.catbox.moe/3dep3s.mp3", IsVideo = false, Type = SongLinkType.Catbox
                    }
                },
            Sources = new List<SongSource>()
            {
                new SongSource()
                {
                    AirDateStart = DateTime.Now,
                    SongType = SongSourceSongType.OP,
                    LanguageOriginal = "ja",
                    Type = SongSourceType.VN,
                    Links = new List<SongSourceLink>()
                    {
                        new SongSourceLink() { Type = SongSourceLinkType.VNDB, Url = "https://vndb.org/v10680" }
                    },
                    Titles =
                        new List<Title>()
                        {
                            new Title()
                            {
                                LatinTitle = "Tsuki ni Yorisou Otome no Sahou",
                                Language = "ja",
                                NonLatinTitle = "月に寄りそう乙女の作法",
                                IsMainTitle = true
                            },
                            new Title() { LatinTitle = "Tsuriotsu", Language = "en" }
                        },
                    Categories = new List<SongSourceCategory>()
                    {
                        new SongSourceCategory() { Name = "cat1", Type = SongSourceCategoryType.Tag },
                        new SongSourceCategory() { Name = "cat2", Type = SongSourceCategoryType.Genre }
                    }
                },
            }
        };

        int mId = await DbManager.InsertSong(song);
        Console.WriteLine($"Inserted mId {mId}");
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteJson()
    {
        await File.WriteAllTextAsync("autocomplete.json", await DbManager.SelectAutocomplete());
    }

    // [Test, Explicit]
    // public async Task ImportVndbData()
    // {
    //     var musicSources =
    //         JsonConvert.DeserializeObject<List<dynamic>>(
    //             await File.ReadAllTextAsync("C:\\emq\\vndb\\music_source.json"))!;
    //     foreach (dynamic dyn in musicSources)
    //     {
    //         await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
    //         await connection.OpenAsync();
    //         await using (var transaction = await connection.BeginTransactionAsync())
    //         {
    //             var musicSource = new MusicSource()
    //             {
    //                 language_original = dyn.olang, rating_average = dyn.c_average, type = (int)SongSourceType.VN,
    //                 // air_date_start = dyn.; // todo
    //             };
    //
    //             var msId = connection.InsertAsync(musicSource);
    //
    //             // await transaction.CommitAsync();
    //         }
    //     }
    //
    //     Console.WriteLine(musicSources);
    // }

    [Test, Explicit]
    public async Task ImportVndbData()
    {
        await VndbImporter.ImportVndbData();
    }
}
