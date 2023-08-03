using System.Linq;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Server.Db;
using NUnit.Framework;

namespace Tests;

public class ConverterTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test_Gsen_ja()
    {
        var songs = (await DbManager.FindSongsBySongSourceTitle("G-senjou no Maou")).ToList();
        var title = Converters.GetSingleTitle(songs.First().Sources.First().Titles
            .OrderByDescending(x => x.NonLatinTitle == null));

        Assert.That(title.LatinTitle == "G-senjou no Maou");
        Assert.That(title.NonLatinTitle == "G線上の魔王");
    }

    [Test]
    public async Task Test_Gsen_en()
    {
        var songs = (await DbManager.FindSongsBySongSourceTitle("G-senjou no Maou")).ToList();
        var title = Converters.GetSingleTitle(songs.First().Sources.First().Titles
            .OrderByDescending(x => x.NonLatinTitle != null), "en", "ja");

        Assert.That(title.LatinTitle == "G-senjou no Maou - The Devil on G-String");
        Assert.That(title.NonLatinTitle == null);
    }

    [Test]
    public async Task Test_MuvLuv_ja()
    {
        var songs = (await DbManager.FindSongsBySongSourceTitle("Muv-Luv")).ToList();
        songs.RemoveAll(x => x.Sources.Any(y => y.Titles.Any(z => z.LatinTitle.Contains("Alternative"))));
        var title = Converters.GetSingleTitle(songs.First().Sources.First().Titles
            .OrderByDescending(x => x.NonLatinTitle == null));

        Assert.That(title.LatinTitle == "Muv-Luv");
        Assert.That(title.NonLatinTitle == "マブラヴ");
    }

    [Test]
    public async Task Test_MuvLuv_en()
    {
        var songs = (await DbManager.FindSongsBySongSourceTitle("Muv-Luv")).ToList();
        songs.RemoveAll(x => x.Sources.Any(y => y.Titles.Any(z => z.LatinTitle.Contains("Alternative"))));
        var title = Converters.GetSingleTitle(songs.First().Sources.First().Titles
            .OrderByDescending(x => x.NonLatinTitle != null), "en", "ja");

        Assert.That(title.LatinTitle == "Muv-Luv");
        Assert.That(title.NonLatinTitle == null);
    }
}
