using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Shared.Core;
using NUnit.Framework;

namespace Tests;

public class MediaAnalyserTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test_Analyse_Batch()
    {
        const string baseDownloadDir = "C:\\emq\\emqsongsbackup";

        Dictionary<string, MediaAnalyserResult> invalidFiles = new();

        string[] filePaths = Directory.GetFiles(baseDownloadDir);
        foreach (string filePath in filePaths)
        {
            var result = await MediaAnalyser.Analyse(filePath);
            bool isValid = result.IsValid;
            if (!isValid)
            {
                invalidFiles.Add(filePath, result);
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(invalidFiles, Utils.JsoIndented));
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
}
