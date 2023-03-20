using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
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
                Assert.That(songLink.Duration.TotalMilliseconds > 0);
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
    public async Task Test_FindSongsByLabels()
    {
        PlayerVndbInfo vndbInfo = new PlayerVndbInfo()
        {
            VndbId = "u101804",
            VndbApiToken = "",
            Labels = new List<Label>
            {
                new()
                {
                    Id = 7, // Voted
                    Kind = LabelKind.Include
                },
            }
        };

        var labels = await VndbMethods.GrabPlayerVNsFromVndb(vndbInfo);
        var songs = await DbManager.FindSongsByLabels(labels);
        Assert.That(songs.Count > 0);
        GenericSongsAssert(songs);
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

        var songs = await DbManager.GetRandomSongs(int.MaxValue, false,
            filters: new QuizFilters { CategoryFilters = categories }, printSql: true);
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

        var songs = await DbManager.GetRandomSongs(int.MaxValue, false,
            filters: new QuizFilters { CategoryFilters = categories }, printSql: true);
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

        var songs = await DbManager.GetRandomSongs(int.MaxValue, false,
            filters: new QuizFilters { CategoryFilters = categories }, printSql: true);
        GenericSongsAssert(songs);

        Assert.That(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Sorcery Jokers")))));

        Assert.That(!(songs.Any(song => song.Sources.Any(source =>
            source.Titles.Any(title => title.LatinTitle.Contains("Magical Charming"))))));

        Assert.That(songs.Count > 4);
        Assert.That(songs.Count < 100);
    }

    [Test]
    public async Task Test_GetRandomSongs_SongSourceSongTypeFilter_OPOrED()
    {
        Dictionary<SongSourceSongType, bool> validSongSourceSongTypes =
            new() { { SongSourceSongType.OP, true }, { SongSourceSongType.ED, true } };

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { SongSourceSongTypeFilters = validSongSourceSongTypes }, printSql: true);

        Assert.That(songs.All(song =>
            song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.OP) ||
            song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.ED)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_SongSourceSongTypeFilter_Insert()
    {
        Dictionary<SongSourceSongType, bool> validSongSourceSongTypes =
            new() { { SongSourceSongType.Insert, true } };

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { SongSourceSongTypeFilters = validSongSourceSongTypes }, printSql: true);

        Assert.That(songs.All(song =>
            song.Sources.SelectMany(x => x.SongTypes).Contains(SongSourceSongType.Insert)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_DateFilter()
    {
        var startDate = new DateTime(1990, 1, 1);
        var endDate = new DateTime(1997, 1, 1);

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { StartDateFilter = startDate, EndDateFilter = endDate }, printSql: true);

        Assert.That(songs.Count > 0);
        Assert.That(songs.Count < 200);
        Assert.That(songs.All(song =>
            song.Sources.Select(x => x.AirDateStart).Any(x => x >= startDate) &&
            song.Sources.Select(x => x.AirDateStart).Any(x => x <= endDate)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_RatingAverageFilter()
    {
        int ratingAverageStart = 920;
        int ratingAverageEnd = 1000;

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { RatingAverageStart = ratingAverageStart, RatingAverageEnd = ratingAverageEnd },
            printSql: true);

        Assert.That(songs.Count > 0);
        Assert.That(songs.Count < 200);
        Assert.That(songs.All(song =>
            song.Sources.Select(x => x.RatingAverage).Any(x => x >= ratingAverageStart) &&
            song.Sources.Select(x => x.RatingAverage).Any(x => x <= ratingAverageEnd)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_RatingBayesianFilter()
    {
        int ratingBayesianStart = 810;
        int ratingBayesianEnd = 870;

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters
            {
                RatingBayesianStart = ratingBayesianStart, RatingBayesianEnd = ratingBayesianEnd
            },
            printSql: true);

        Assert.That(songs.Count > 0);
        Assert.That(songs.Count < 1000);
        Assert.That(songs.All(song =>
            song.Sources.Select(x => x.RatingBayesian).Any(x => x >= ratingBayesianStart) &&
            song.Sources.Select(x => x.RatingBayesian).Any(x => x <= ratingBayesianEnd)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_RatingPopularityFilter()
    {
        int popularityStart = 7700;
        int popularityEnd = 10000;

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { PopularityStart = popularityStart, PopularityEnd = popularityEnd },
            printSql: true);

        Assert.That(songs.Count > 0);
        Assert.That(songs.Count < 100);
        Assert.That(songs.All(song =>
            song.Sources.Select(x => x.Popularity).Any(x => x >= popularityStart) &&
            song.Sources.Select(x => x.Popularity).Any(x => x <= popularityEnd)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_RatingPopularityFilter_0()
    {
        int popularityStart = 0;
        int popularityEnd = 0;

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { PopularityStart = popularityStart, PopularityEnd = popularityEnd },
            printSql: true);

        Assert.That(songs.Count > 100);
        Assert.That(songs.Count < 10000);
        Assert.That(songs.All(song =>
            song.Sources.Select(x => x.Popularity).Any(x => x >= popularityStart) &&
            song.Sources.Select(x => x.Popularity).Any(x => x <= popularityEnd)));
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_RatingVoteCountFilter()
    {
        int voteCountStart = 3000;
        int voteCountEnd = 4000;

        var songs = await DbManager.GetRandomSongs(int.MaxValue, true,
            filters: new QuizFilters { VoteCountStart = voteCountStart, VoteCountEnd = voteCountEnd },
            printSql: true);

        Assert.That(songs.Count > 0);
        Assert.That(songs.Count < 200);
        Assert.That(songs.All(song =>
            song.Sources.Select(x => x.VoteCount).Any(x => x >= voteCountStart) &&
            song.Sources.Select(x => x.VoteCount).Any(x => x <= voteCountEnd)));
        GenericSongsAssert(songs);
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

        Assert.That(libraryStats.TotalLibraryStatsMusicType.First().MusicCount > 0);
        Assert.That(libraryStats.AvailableLibraryStatsMusicType.First().MusicCount > 0);

        Assert.That(libraryStats.VideoLinkCount > 0);
        Assert.That(libraryStats.SoundLinkCount > 0);
        Assert.That(libraryStats.BothLinkCount > 0);

        Assert.That(libraryStats.msm.First().MSId > 0);
        Assert.That(libraryStats.msmAvailable.First().MSId > 0);

        Assert.That(libraryStats.am.First().AId > 0);
        Assert.That(libraryStats.amAvailable.First().AId > 0);

        Assert.That(libraryStats.msYear.First().Value > 0);
        Assert.That(libraryStats.msYearAvailable.ElementAtOrDefault(10).Value > 0);
        Assert.That(libraryStats.msYear.Keys.Count == libraryStats.msYearAvailable.Keys.Count);
    }

    [Test]
    public async Task Test_SelectLibraryStatsYoneMado()
    {
        var libraryStats = await DbManager.SelectLibraryStats(int.MaxValue);
        var yoneMado = libraryStats.am.Single(x => x.AALatinAlias == "Yonezawa Madoka");
        Console.WriteLine(yoneMado.MusicCount);
        Assert.That(yoneMado.MusicCount > 14);
    }

    [Test, Explicit]
    public async Task Test_MusicSourceDuplicationThing()
    {
        const string json =
            "[{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Kimi to Aruku, Kaze ga Warau\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s240\",\"Titles\":[{\"LatinTitle\":\"Tooyama Eriko\",\"NonLatinTitle\":\"\\u9060\\u5C71 \\u679D\\u91CC\\u5B50\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Warp Out Love!\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s240\",\"Titles\":[{\"LatinTitle\":\"Tooyama Eriko\",\"NonLatinTitle\":\"\\u9060\\u5C71 \\u679D\\u91CC\\u5B50\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Thesis ~Meidai~\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s656\",\"Titles\":[{\"LatinTitle\":\"Kono Kanami\",\"NonLatinTitle\":\"\\u3053\\u306E \\u304B\\u306A\\u307F\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Hashire! Wagamama Heart\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[1],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Buchikamase! WAGAMAMA Heart\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Haikaburi Hime Janakute mo\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"In Pain/In Vein\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Kiitete Nee.\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Usotsuki to Kizuato\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]},{\"Id\":0,\"AirDateStart\":\"2013-01-25T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":722,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Gin no Toki no Corona -Fortune Dragon\\u0027s-\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u9280\\u306E\\u523B\\u306E\\u30B3\\u30ED\\u30CA -Fortune Dragon\\u2019s-\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v11316\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Rakuen no Distance\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s13152\",\"Titles\":[{\"LatinTitle\":\"Urasawa Tamaki\",\"NonLatinTitle\":\"\\u6D66\\u6CA2 \\u74B0\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[1],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Ano Tenkuu e Fly Away!\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s13152\",\"Titles\":[{\"LatinTitle\":\"Urasawa Tamaki\",\"NonLatinTitle\":\"\\u6D66\\u6CA2 \\u74B0\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[2],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Bokutachi no Monogatari\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s13152\",\"Titles\":[{\"LatinTitle\":\"Urasawa Tamaki\",\"NonLatinTitle\":\"\\u6D66\\u6CA2 \\u74B0\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2009-01-23T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":741,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Yumina the Ethereal\",\"NonLatinTitle\":null,\"Language\":\"en\",\"IsMainTitle\":false},{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true},{\"LatinTitle\":\"Huiguangyi Zhanji\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6230\\u8A18\",\"Language\":\"zh-Hant\",\"IsMainTitle\":false}],\"Links\":[{\"Url\":\"https://vndb.org/v1155\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[2],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Meltin\\u0027 Heart\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s516\",\"Titles\":[{\"LatinTitle\":\"Misaki Rina\",\"NonLatinTitle\":\"\\u4E09\\u54B2 \\u91CC\\u5948\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2010-01-29T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":772,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina FD  -ForeverDreams-\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CAFD -ForeverDreams-\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v2886\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[2],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Forever Dreams\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2010-01-29T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":772,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Tenkuu no Yumina FD  -ForeverDreams-\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u5929\\u7A7A\\u306E\\u30E6\\u30DF\\u30CAFD -ForeverDreams-\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v2886\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[1],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"69 Oku no Yoake\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s359\",\"Titles\":[{\"LatinTitle\":\"Toono Soyogi\",\"NonLatinTitle\":\"\\u9060\\u91CE \\u305D\\u3088\\u304E\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2011-12-16T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":790,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Gin no Toki no Corona\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u9280\\u306E\\u523B\\u306E\\u30B3\\u30ED\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v6668\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[2],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Wonder Tokimeki\\u2606System\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s955\",\"Titles\":[{\"LatinTitle\":\"Mizukiri Keito\",\"NonLatinTitle\":\"\\u6C34\\u9727 \\u3051\\u3044\\u3068\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2011-12-16T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":790,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Gin no Toki no Corona\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u9280\\u306E\\u523B\\u306E\\u30B3\\u30ED\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v6668\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[3],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Shukumei no Pawn\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s1847\",\"Titles\":[{\"LatinTitle\":\"Riryka\",\"NonLatinTitle\":\"\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2011-12-16T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":790,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Gin no Toki no Corona\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u9280\\u306E\\u523B\\u306E\\u30B3\\u30ED\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v6668\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[1],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Michita Koku no Spica\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s1847\",\"Titles\":[{\"LatinTitle\":\"Riryka\",\"NonLatinTitle\":\"\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2011-12-16T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":790,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Gin no Toki no Corona\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u9280\\u306E\\u523B\\u306E\\u30B3\\u30ED\\u30CA\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v6668\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[1],\"MusicIds\":[]}]},{\"Id\":0,\"Length\":-1,\"StartTime\":0,\"Titles\":[{\"LatinTitle\":\"Split Second\",\"NonLatinTitle\":null,\"Language\":\"ja\",\"IsMainTitle\":true}],\"Artists\":[{\"Id\":0,\"PrimaryLanguage\":\"ja\",\"Sex\":1,\"VndbId\":\"s13153\",\"Titles\":[{\"LatinTitle\":\"CAO\",\"NonLatinTitle\":\"\",\"Language\":\"ja\",\"IsMainTitle\":false}],\"Role\":1,\"MusicIds\":[]}],\"Links\":[],\"Type\":1,\"Sources\":[{\"Id\":0,\"AirDateStart\":\"2013-01-25T00:00:00\",\"AirDateEnd\":null,\"LanguageOriginal\":\"ja\",\"RatingAverage\":722,\"Type\":1,\"Titles\":[{\"LatinTitle\":\"Kikouyoku Senki Gin no Toki no Corona -Fortune Dragon\\u0027s-\",\"NonLatinTitle\":\"\\u8F1D\\u5149\\u7FFC\\u6226\\u8A18 \\u9280\\u306E\\u523B\\u306E\\u30B3\\u30ED\\u30CA -Fortune Dragon\\u2019s-\",\"Language\":\"ja\",\"IsMainTitle\":true}],\"Links\":[{\"Url\":\"https://vndb.org/v11316\",\"Type\":1}],\"Categories\":[],\"SongTypes\":[1],\"MusicIds\":[]}]}]";

        var songs = JsonSerializer.Deserialize<List<Song>>(json)!;
        foreach (var song in songs)
        {
            int _ = await DbManager.InsertSong(song);
        }
    }


    [Test, Explicit]
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

    [Test, Explicit]
    public async Task Test_FilterSongLinks()
    {
        var song = (await DbManager.SelectSongs(new Song
        {
            Titles = new List<Title> { new() { LatinTitle = "Mirage Lullaby" } }
        })).Single();

        var filtered = SongLink.FilterSongLinks(song.Links);
        Assert.That(filtered.Count == 1);
    }
}
