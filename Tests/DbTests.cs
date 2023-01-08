using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Newtonsoft.Json;
using Npgsql;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

public class DbTests
{
    [SetUp]
    public void Setup()
    {
    }

    private static void GenericSongsAssert(IEnumerable<Song> songs)
    {
        foreach (Song song in songs)
        {
            Assert.That(song.Id > 0);

            Assert.That(song.Titles.First().LatinTitle.Any());
            Assert.That(song.Titles.First().Language.Any());
            Assert.That(song.Titles.Any(x => x.IsMainTitle));

            // Assert.That(song.Links.First().Url.Any());

            Assert.That(song.Sources.First().Id > 0);
            Assert.That(song.Sources.First().Titles.First().LatinTitle.Any());
            Assert.That(song.Sources.First().Titles.First().Language.Any());
            Assert.That(song.Sources.First().Titles.Any(x => x.IsMainTitle));
            Assert.That(song.Sources.First().Links.First().Url.Any());
            Assert.That(song.Sources.First().SongTypes.Any());

            // Assert.That(song.Sources.First().Categories.First().Name.Any());

            Assert.That(song.Artists.First().Id > 0);
            Assert.That(song.Artists.First().Titles.First().LatinTitle.Any());
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

        Assert.That(songs.Count == 3);
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_SelectSongs_YuminaMainTitleThing()
    {
        var songs = await DbManager.SelectSongs(new Song() { Id = 821, });
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_FindSongsBySongSourceTitle_MultipleSongSourceTypes()
    {
        var songs = await DbManager.FindSongsBySongSourceTitle("Yoake Mae yori Ruri Iro na");
        var song = songs.First(x => x.Titles.Any(y => y.LatinTitle == "WAX & WANE"));

        GenericSongsAssert(new List<Song> { song });
    }

    [Test]
    public async Task Test_FindSongsByArtistTitle_KOTOKO()
    {
        var songs = await DbManager.FindSongsByArtistTitle("KOTOKO");

        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_FindSongsByArtistTitle_ByNonMainAlias()
    {
        var songs = (await DbManager.FindSongsByArtistTitle("Mishiro Mako")).ToList();

        Assert.That(songs.Count > 10);
        Assert.That(songs.Count < 30);
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_100()
    {
        var songs = await DbManager.GetRandomSongs(100, true);

        Assert.That(songs.Count > 99);
        GenericSongsAssert(songs);
    }

    [Test]
    public async Task Test_GetRandomSongs_IsDistinct()
    {
        var songs = await DbManager.GetRandomSongs(int.MaxValue, true);

        HashSet<int> seen = new();
        foreach (Song song in songs)
        {
            bool added = seen.Add(song.Id);
            if (!added)
            {
                throw new Exception();
            }
        }

        GenericSongsAssert(songs);
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
                    AirDateStart = DateTime.Now,
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
    public async Task GenerateAutocompleteJson()
    {
        await File.WriteAllTextAsync("autocomplete.json", await DbManager.SelectAutocomplete());
    }

    [Test, Explicit]
    public async Task ImportVndbData()
    {
        await VndbImporter.ImportVndbData();
    }

    [Test, Explicit]
    public async Task GenerateSong()
    {
        await File.WriteAllTextAsync("Song.json", await DbManager.ExportSong());
    }

    [Test, Explicit]
    public async Task GenerateSongLite()
    {
        await File.WriteAllTextAsync("SongLite.json", await DbManager.ExportSongLite());
    }

    [Test, Explicit]
    public async Task GenerateReviewQueue()
    {
        await File.WriteAllTextAsync("ReviewQueue.json", await DbManager.ExportReviewQueue());
    }

    [Test, Explicit]
    public async Task ImportSongLite()
    {
        var deserialized =
            JsonConvert.DeserializeObject<List<SongLite>>(
                await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\SongLite.json"));
        await DbManager.ImportSongLite(deserialized!);
    }

    // music ids can change between vndb imports, so this doesn't work correctly right now
    // [Test, Explicit]
    // public async Task ImportReviewQueue()
    // {
    //     var deserialized =
    //         JsonConvert.DeserializeObject<List<ReviewQueue>>(
    //             await File.ReadAllTextAsync("C:\\emq\\emqsongsmetadata\\ReviewQueue.json"));
    //     await DbManager.ImportReviewQueue(deserialized!);
    // }

    [Test, Explicit]
    public async Task ApproveReviewQueueItem()
    {
        var rqIds = Enumerable.Range(56, 1).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Approved);
        }
    }

    [Test, Explicit]
    public async Task RejectReviewQueueItem()
    {
        var rqIds = Enumerable.Range(24, 1).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Rejected);
        }
    }

    [Test, Explicit]
    public async Task Test_GrabVnsFromVndb()
    {
        // todo move this to another class?
        PlayerVndbInfo vndbInfo = new PlayerVndbInfo()
        {
            VndbId = "u101804",
            VndbApiToken = "",
            Labels = new List<Label>
            {
                new()
                {
                    Id = 2, // Finished
                    Kind = LabelKind.Include
                },
                new()
                {
                    Id = 7, // Voted
                    Kind = LabelKind.Include
                },
                new()
                {
                    Id = 6, // Blacklist
                    Kind = LabelKind.Exclude
                },
            }
        };

        var labels = await VndbMethods.GrabPlayerVNsFromVndb(vndbInfo);
        Console.WriteLine(JsonSerializer.Serialize(labels, Utils.Jso));
        Assert.That(labels.Count > 1);
        Assert.That(labels.First().VnUrls.Count > 1);
    }

    [Test, Explicit]
    public async Task BackupSongFilesUsingSongLite()
    {
        string songLitePath = "C:\\emq\\emqsongsmetadata\\SongLite.json";
        var songLites =
            JsonSerializer.Deserialize<List<SongLite>>(await File.ReadAllTextAsync(songLitePath), Utils.JsoIndented)!;

        var client = new HttpClient();

        int dlCount = 0;
        const int waitMs = 5000;

        foreach (var songLite in songLites)
        {
            foreach (var link in songLite.Links)
            {
                var directory = "C:\\emq\\emqsongsbackup";
                var filePath = $"{directory}\\{new Uri(link.Url).Segments.Last()}";

                if (!File.Exists(filePath))
                {
                    var stream = await client.GetStreamAsync(link.Url);

                    await using (MemoryStream ms = new())
                    {
                        await stream.CopyToAsync(ms);
                        Directory.CreateDirectory(directory);
                        await File.WriteAllBytesAsync(filePath, ms.ToArray());
                    }

                    dlCount += 1;
                    await Task.Delay(waitMs);
                }
            }
        }

        Console.WriteLine($"Downloaded {dlCount} files.");
    }

    // todo pgrestore pgdump tests
}
