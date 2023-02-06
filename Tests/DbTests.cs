using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using NUnit.Framework;

namespace Tests;

public class DbTests
{
    [SetUp]
    public void Setup()
    {
    }

    private static void GenericSongsAssert(List<Song> songs)
    {
        Console.WriteLine($"{songs.Count} songs");

        // Assert.That(songs.Any(x => x.Sources.Any(y => y.Categories.Any())));
        foreach (Song song in songs)
        {
            Assert.That(song.Id > 0);

            Assert.That(song.Titles.First().LatinTitle.Any());
            Assert.That(song.Titles.First().Language.Any());
            Assert.That(song.Titles.Any(x => x.IsMainTitle));

            foreach (SongLink songLink in song.Links)
            {
                Assert.That(!string.IsNullOrWhiteSpace(songLink.Url));
                Assert.That(songLink.Type != SongLinkType.Unknown);
            }

            foreach (SongSource songSource in song.Sources)
            {
                Assert.That(songSource.Id > 0);
                Assert.That(songSource.Titles.First().LatinTitle.Any());
                Assert.That(songSource.Titles.First().Language.Any());
                Assert.That(songSource.Titles.Any(x => x.IsMainTitle));
                Assert.That(songSource.Links.First().Url.Any());
                Assert.That(songSource.SongTypes.Any());

                HashSet<int> seenTags = new();
                foreach (SongSourceCategory songSourceCategory in songSource.Categories)
                {
                    Assert.That(songSourceCategory.Id > 0);
                    Assert.That(songSourceCategory.Name.Any());
                    Assert.That(songSourceCategory.Type != SongSourceCategoryType.Unknown);

                    if (songSourceCategory.Type == SongSourceCategoryType.Tag)
                    {
                        Assert.That(songSourceCategory.Rating > 0);
                        Assert.That(songSourceCategory.SpoilerLevel is not null);

                        bool added = seenTags.Add(songSourceCategory.Id);
                        if (!added)
                        {
                            throw new Exception();
                        }
                    }
                }
            }

            foreach (SongArtist songArtist in song.Artists)
            {
                Assert.That(songArtist.Id > 0);
                Assert.That(songArtist.Titles.First().LatinTitle.Any());
            }
        }
    }

    [Test]
    public async Task Test_SelectSongs_ByTitles()
    {
        var songs = await DbManager.SelectSongs(new Song()
        {
            // Id = 210,
            Titles = new List<Title>
            {
                new() { LatinTitle = "Restoration ~Chinmoku no Sora~" }, new() { LatinTitle = "SHOOTING STAR" }
            }
        });
        GenericSongsAssert(songs);

        Assert.That(songs.Count == 3);
    }

    [Test]
    public async Task Test_SelectSongs_YuminaMainTitleThing()
    {
        var songs = (await DbManager.SelectSongs(new Song() { Id = 821, })).ToList();
        GenericSongsAssert(songs);

        Assert.That(songs.Count > 0);
    }

    [Test]
    public async Task Test_FindSongsBySongSourceTitle_MultipleSongSourceTypes()
    {
        var songs = (await DbManager.FindSongsBySongSourceTitle("Yoake Mae yori Ruri Iro na")).ToList();

        var song = songs.First(x => x.Titles.Any(y => y.LatinTitle == "WAX & WANE"));
        GenericSongsAssert(new List<Song> { song });
    }

    [Test]
    public async Task Test_FindSongsBySongSourceCategories()
    {
        var categories = new List<SongSourceCategory>()
        {
            new() { VndbId = "g2405", Type = SongSourceCategoryType.Tag },
            new() { VndbId = "g2689", Type = SongSourceCategoryType.Tag },
        };
        var songs = (await DbManager.FindSongsBySongSourceCategories(categories)).ToList();
        GenericSongsAssert(songs);

        Assert.That(songs.Any(x => x.Sources.Any(y => y.Categories.Any(z => z.Name == "Non Looping BGM"))));
        Assert.That(songs.Any(x => x.Sources.Any(y => y.Categories.Any(z => z.Name == "Modifications"))));
    }

    [Test]
    public async Task Test_FindSongsByArtistTitle_KOTOKO()
    {
        var songs = (await DbManager.FindSongsByArtistTitle("KOTOKO")).ToList();
        GenericSongsAssert(songs);

        Assert.That(songs.Count > 100);
    }

    [Test]
    public async Task Test_FindSongsByArtistTitle_ByNonMainAlias()
    {
        var songs = (await DbManager.FindSongsByArtistTitle("Mishiro Mako")).ToList();
        GenericSongsAssert(songs);

        Assert.That(songs.Count > 10);
        Assert.That(songs.Count < 30);
    }

    [Test]
    public async Task Test_GetRandomSongs_100()
    {
        var songs = await DbManager.GetRandomSongs(100, true);
        GenericSongsAssert(songs);

        Assert.That(songs.Count > 99);
    }

    [Test]
    public async Task Test_GetRandomSongs_100000()
    {
        var songs = await DbManager.GetRandomSongs(100000, true);
        GenericSongsAssert(songs);

        Assert.That(songs.Count > 400);
    }

    [Test]
    public async Task Test_GetRandomSongs_100000_NoDuplicates()
    {
        var songs = await DbManager.GetRandomSongs(100000, false);
        GenericSongsAssert(songs);

        Assert.That(songs.Count > 280);
    }

    [Test]
    public async Task Test_GetRandomSongs_CategoryFilter_1()
    {
        List<CategoryFilter> categories = new()
        {
            // (ignored) ~ da capo 3, duel savior, Justy×Nasty ~Maou Hajimemashita~, magical charming, sorcery jokers, edelweiss
            new CategoryFilter(new SongSourceCategory() { VndbId = "g685", SpoilerLevel = SpoilerLevel.None },
                LabelKind.Maybe) { },
            // + a lot of shit
            new CategoryFilter(new SongSourceCategory() { VndbId = "g424", SpoilerLevel = SpoilerLevel.Minor },
                LabelKind.Include) { },
        };

        var songs = await DbManager.GetRandomSongs(100000, false, validCategories: categories, printSql: true);
        GenericSongsAssert(songs);

        Assert.That(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Ano Harewataru Sora yori Takaku")))));

        Assert.That(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Ao no Kanata no Four Rhythm")))));

        Assert.That(songs.Count > 10);
        Assert.That(songs.Count < 400);
    }


    [Test]
    public async Task Test_GetRandomSongs_CategoryFilter_1_NoAokana()
    {
        List<CategoryFilter> categories = new()
        {
            // (ignored) ~ da capo 3, duel savior, Justy×Nasty ~Maou Hajimemashita~, magical charming, sorcery jokers, edelweiss
            new CategoryFilter(new SongSourceCategory() { VndbId = "g685", SpoilerLevel = SpoilerLevel.None },
                LabelKind.Maybe) { },
            // + a lot of shit
            new CategoryFilter(new SongSourceCategory() { VndbId = "g424", SpoilerLevel = SpoilerLevel.Minor },
                LabelKind.Include) { },
            // - aokana
            new CategoryFilter(new SongSourceCategory() { VndbId = "g2924", SpoilerLevel = SpoilerLevel.Major },
                LabelKind.Exclude) { },
        };

        var songs = await DbManager.GetRandomSongs(100000, false, validCategories: categories, printSql: true);
        GenericSongsAssert(songs);

        Assert.That(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Ano Harewataru Sora yori Takaku")))));

        Assert.That(!(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Ao no Kanata no Four Rhythm"))))));

        Assert.That(songs.Count > 10);
        Assert.That(songs.Count < 400);
    }

    [Test]
    public async Task Test_GetRandomSongs_CategoryFilter_2()
    {
        List<CategoryFilter> categories = new()
        {
            // - magical charming
            new CategoryFilter(new SongSourceCategory() { VndbId = "g187", SpoilerLevel = SpoilerLevel.Major },
                LabelKind.Exclude) { },
            // ~ da capo 3, duel savior, Justy×Nasty ~Maou Hajimemashita~, magical charming, sorcery jokers, edelweiss
            new CategoryFilter(new SongSourceCategory() { VndbId = "g685", SpoilerLevel = SpoilerLevel.None },
                LabelKind.Maybe),
            // ~ aokana,
            new CategoryFilter(new SongSourceCategory() { VndbId = "g2924", SpoilerLevel = SpoilerLevel.None },
                LabelKind.Maybe),
        };

        var songs = await DbManager.GetRandomSongs(100000, false, validCategories: categories, printSql: true);
        GenericSongsAssert(songs);

        Assert.That(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Sorcery Jokers")))));

        Assert.That(!(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Magical Charming"))))));

        Assert.That(songs.Count > 4);
        Assert.That(songs.Count < 100);
    }

    [Test]
    public async Task Test_GetRandomSongs_IsDistinct()
    {
        var songs = await DbManager.GetRandomSongs(int.MaxValue, true);
        GenericSongsAssert(songs);

        HashSet<int> seen = new();
        foreach (Song song in songs)
        {
            bool added = seen.Add(song.Id);
            if (!added)
            {
                throw new Exception();
            }
        }
    }

    [Test]
    public async Task Test_SelectLibraryStats()
    {
        var libraryStats = await DbManager.SelectLibraryStats();
        Console.WriteLine(JsonSerializer.Serialize(libraryStats, Utils.Jso));

        Assert.That(libraryStats.TotalMusicCount > 0);
        Assert.That(libraryStats.TotalMusicSourceCount > 0);
        Assert.That(libraryStats.TotalArtistCount > 0);

        Assert.That(libraryStats.VideoLinkCount > 0);
        Assert.That(libraryStats.SoundLinkCount > 0);
        Assert.That(libraryStats.BothLinkCount > 0);

        Assert.That(libraryStats.msm.First().MId > 0);
        Assert.That(libraryStats.msmAvailable.First().MId > 0);

        Assert.That(libraryStats.am.First().AId > 0);
        Assert.That(libraryStats.amAvailable.First().AId > 0);
    }

    [Test, Explicit]
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
                    AirDateStart = DateTime.UtcNow,
                    SongTypes = new List<SongSourceSongType> { SongSourceSongType.OP },
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
}
