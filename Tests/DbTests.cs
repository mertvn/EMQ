using System.Collections.Generic;
using System.Linq;
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
        Assert.That(song.LatinTitle.Any());
        Assert.That(song.Links.First().Url.Any());
        Assert.That(song.Sources.First().Aliases.Any());
        Assert.That(song.Artists.First().Aliases.Any());
    }

    [Test]
    public async Task Test_SelectSong_7()
    {
        var song = await DbManager.SelectSong(7);
        Assert.That(song.Id > 0);
        Assert.That(song.LatinTitle.Any());
        Assert.That(song.Links.First().Url.Any());
        Assert.That(song.Sources.First().Aliases.Any());
        Assert.That(song.Artists.First().Aliases.Any());
    }

    [Test]
    public async Task Test_GetRandomSongs_100()
    {
        var songs = await DbManager.GetRandomSongs(2);

        foreach (Song song in songs)
        {
            Assert.That(song.Id > 0);
            Assert.That(song.LatinTitle.Any());
            Assert.That(song.Links.First().Url.Any());
            Assert.That(song.Sources.First().Aliases.Any());
            Assert.That(song.Artists.First().Aliases.Any());
        }
    }

    [Test]
    public async Task Test_InsertSong()
    {
        var song = new Song()
        {
            LatinTitle = "Desire",
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
                    Aliases = new List<string>() { "Tsuki ni Yorisou Otome no Sahou", "Tsuriotsu" }
                }
            }
        };
        await DbManager.InsertSong(song);
    }
}
