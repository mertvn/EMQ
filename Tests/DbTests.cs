using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorApp1.Server.Db;
using BlazorApp1.Shared.Quiz.Entities.Concrete;
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
        Assert.That(song.Links.First().Url.Any());
        Assert.That(song.Sources.First().Aliases.First().Any);
        Assert.That(song.Sources.First().Categories.First().Name.Any());
        Assert.That(song.Artists.First().Aliases.First().Any());
    }

    [Test]
    public async Task Test_SelectSong_7()
    {
        var song = await DbManager.SelectSong(7);
        Assert.That(song.Id > 0);
        Assert.That(song.Titles.First().LatinTitle.Any());
        Assert.That(song.Links.First().Url.Any());
        Assert.That(song.Sources.First().Aliases.First().Any);
        Assert.That(song.Sources.First().Categories.First().Name.Any());
        Assert.That(song.Artists.First().Aliases.First().Any());
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
            Assert.That(song.Sources.First().Aliases.Any());
            Assert.That(song.Sources.First().Categories.First().Name.Any());
            Assert.That(song.Artists.First().Aliases.First().Any());
        }
    }

    [Test]
    public async Task Test_InsertSong()
    {
        var song = new Song()
        {
            Titles = new List<SongTitle>() { new SongTitle() { LatinTitle = "Desire" } },
            Artists = new List<SongArtist>() { new SongArtist() { Aliases = new List<string>() { "Misato Aki" } } },
            Links =
                new List<SongLink>()
                {
                    new SongLink() { Url = "https://files.catbox.moe/3dep3s.mp3", IsVideo = false }
                },
            Sources = new List<SongSource>()
            {
                new SongSource()
                {
                    Aliases = new List<string>() { "Tsuki ni Yorisou Otome no Sahou", "Tsuriotsu" },
                    Categories = new List<SongSourceCategory>()
                    {
                        new SongSourceCategory() { Name = "cat1", Type = 7 },
                        new SongSourceCategory() { Name = "cat2", Type = 8 }
                    }
                },
            }
        };
        await DbManager.InsertSong(song);
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteJson()
    {
        await File.WriteAllTextAsync("autocomplete.json", await DbManager.SelectAutocomplete());
    }
}
