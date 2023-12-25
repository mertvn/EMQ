using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using NUnit.Framework;

namespace Tests;

[Explicit]
public class MediaAnalyserTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test, Explicit]
    public async Task Test_AnalyseBackupFolder()
    {
        const string baseDownloadDir = "K:\\emq\\emqsongsbackup";

        Dictionary<string, MediaAnalyserResult> results = new();

        string[] filePaths = Directory.GetFiles(baseDownloadDir);
        foreach (string filePath in filePaths)
        {
            var result = await MediaAnalyser.Analyse(filePath);
            results.Add(filePath, result);
        }

        Console.WriteLine(JsonSerializer.Serialize(results.Where(x => !x.Value.IsValid), Utils.JsoIndented));
    }

    [Test, Explicit]
    public async Task Test_AnalyseAndUpdateDurationsOfDbSongs()
    {
        const string baseDownloadDir = "K:\\emq\\emqsongsbackup";
        string[] filePaths = Directory.GetFiles(baseDownloadDir);

        var dbSongs = await DbManager.GetRandomSongs(int.MaxValue, true);
        foreach (Song dbSong in dbSongs)
        {
            foreach (SongLink dbSongLink in dbSong.Links)
            {
                // if (true || dbSongLink.Duration.TotalMilliseconds <= 0)
                if (dbSongLink.Duration.TotalMilliseconds <= 0)
                {
                    var filePath = filePaths.FirstOrDefault(x => x.LastSegment() == dbSongLink.Url.LastSegment());
                    if (filePath != null)
                    {
                        var result = await MediaAnalyser.Analyse(filePath);
                        Assert.That(result.Duration != null);
                        Assert.That(result.Duration.HasValue);
                        Assert.That(result.Duration!.Value.TotalMilliseconds > 0);

                        // if (dbSongLink.Duration != result.Duration.Value)
                        // {
                        // }
                        // else
                        // {
                        //     continue;
                        // }

                        Console.WriteLine($"Setting {dbSongLink.Url} duration to {result.Duration!.Value}");
                        await DbManager.UpdateMusicExternalLinkDuration(dbSongLink.Url, result.Duration!.Value);
                    }
                    else
                    {
                        Console.WriteLine($"Song backup not found: {dbSongLink.Url}");
                    }
                }
            }
        }
    }

    [Test]
    public async Task Test_Analyse_ogg()
    {
        string url = "https://files.catbox.moe/kctgih.ogg";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            bool isValid = (await MediaAnalyser.Analyse(filePath)).IsValid;
            Assert.That(isValid);
        }
        else
        {
            Assert.Fail();
        }
    }

    [Test]
    public async Task Test_Analyse_mp3()
    {
        string url = "https://files.catbox.moe/slj05f.mp3";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            bool isValid = (await MediaAnalyser.Analyse(filePath)).IsValid;
            Assert.That(isValid);
        }
        else
        {
            Assert.Fail();
        }
    }

    [Test]
    public async Task Test_Analyse_mp4()
    {
        string url = "https://files.catbox.moe/e0c1ab.mp4";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            bool isValid = (await MediaAnalyser.Analyse(filePath)).IsValid;
            Assert.That(isValid);
        }
        else
        {
            Assert.Fail();
        }
    }

    [Test, Explicit]
    public async Task Test_Analyse_mp4_FakeVideo()
    {
        string url = "https://files.catbox.moe/dxiisg.mp4";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            var result = await MediaAnalyser.Analyse(filePath);
            bool isFakeVideo = result.Warnings.Contains(MediaAnalyserWarningKind.FakeVideo);
            Assert.That(isFakeVideo);
            Assert.That(!result.IsValid);
        }
        else
        {
            Assert.Fail();
        }
    }

    [Test]
    public async Task Test_Analyse_webm()
    {
        string url = "https://files.catbox.moe/fwpruo.webm";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            bool isValid = (await MediaAnalyser.Analyse(filePath)).IsValid;
            Assert.That(isValid);
        }
        else
        {
            Assert.Fail();
        }
    }

    [Test]
    public async Task Test_Analyse_webm_1000FPS()
    {
        string url = "https://files.catbox.moe/kxh6kx.webm";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            bool isValid = (await MediaAnalyser.Analyse(filePath)).IsValid;
            Assert.That(isValid);
        }
        else
        {
            Assert.Fail();
        }
    }

    [Test]
    public async Task Test_Analyse_mp3_VolumeDetect()
    {
        string url = "https://files.catbox.moe/bv3wtq.mp3";
        string filePath = System.IO.Path.GetTempPath() + url.LastSegment();

        bool success = await ServerUtils.Client.DownloadFile(filePath, new Uri(url));
        if (success)
        {
            var result = await MediaAnalyser.Analyse(filePath);
            bool isValid = result.IsValid;
            Assert.That(isValid);
            Assert.That(result.VolumeDetect != null);
        }
        else
        {
            Assert.Fail();
        }
    }
}
