using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

[Explicit]
public class FPCalcTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test, Explicit]
    public async Task Test_AnalyseBackupFolder()
    {
        // const string baseDownloadDir = "K:\\emq\\emqsongsbackup";
        const string baseDownloadDir = @"L:\olil355 - Copy\FolderI";

        var filePaths = Directory.EnumerateFiles(baseDownloadDir, "*.mp3", SearchOption.AllDirectories);
        foreach (string filePath in filePaths)
        {
            Console.WriteLine(filePath);
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "fpcalc.exe",
                    Arguments = $"\"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string err = await process.StandardError.ReadToEndAsync();

            if (err.Any())
            {
                Console.WriteLine(err);
                throw new Exception();
            }

            var lines = output.Split("\n");
            if (lines.Length < 2 || !lines[0].StartsWith("DURATION") || !lines[1].StartsWith("FINGERPRINT"))
            {
                Console.WriteLine(output);
                throw new Exception();
            }

            var duration = lines[0].Replace("DURATION=", "");
            var fingerprint = lines[1].Replace("FINGERPRINT=", "");

            Console.WriteLine(duration);
            Console.WriteLine(fingerprint);

            await process.WaitForExitAsync();

            var clientId = ""; // todo
            AcoustIDLookup? res = await ServerUtils.Client.GetFromJsonAsync<AcoustIDLookup>(
                $"https://api.acoustid.org/v2/lookup?client={clientId}&meta=recordingids&duration={duration}&fingerprint={fingerprint}");

            var serialized = JsonSerializer.Serialize(res, Utils.JsoIndented);
            Console.WriteLine(serialized);
            if (res != null && res.results.Any(x => x.recordings.Any()))
            {
                return;
            }

            // return;
        }
    }

    class AcoustIDLookup
    {
        public string status { get; set; } = "";

        public AcoustIDLookupResult[] results { get; set; } = Array.Empty<AcoustIDLookupResult>();
    }

    class AcoustIDLookupResult
    {
        public string id { get; set; } = "";

        public float score { get; set; }

        public AcoustIDLookupResultRecording[] recordings { get; set; } = Array.Empty<AcoustIDLookupResultRecording>();
    }

    class AcoustIDLookupResultRecording
    {
        public string id { get; set; } = "";
    }
}
