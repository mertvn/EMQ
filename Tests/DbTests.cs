using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Shared.Quiz.Entities.Concrete;
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

        Assert.That(song.Links.First().Url.Any());

        Assert.That(song.Sources.First().Id > 0);
        Assert.That(song.Sources.First().Titles.First().LatinTitle.Any());
        Assert.That(song.Sources.First().Titles.First().Language.Any());
        Assert.That(song.Sources.First().Links.First().Url.Any());
        Assert.That(song.Sources.First().Categories.First().Name.Any());

        Assert.That(song.Artists.First().Id > 0);
        Assert.That(song.Artists.First().Titles.First().LatinTitle.Any());
    }

    [Test]
    public async Task Test_GetRandomSongs_100()
    {
        var songs = await DbManager.GetRandomSongs(2);

        foreach (Song song in songs)
        {
            Assert.That(song.Id > 0);
            Assert.That(song.Titles.First().LatinTitle.Any());
            Assert.That(song.Links.First().Url.Any());
            Assert.That(song.Sources.First().Titles.First().LatinTitle.Any());
            Assert.That(song.Sources.First().Categories.First().Name.Any());
            Assert.That(song.Artists.First().Titles.First().LatinTitle.Any());
        }
    }

    [Test]
    public async Task Test_InsertSong()
    {
        var song = new Song()
        {
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
                            },
                            new Title() { LatinTitle = "Misato Aki2", Language = "en" }
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
}
