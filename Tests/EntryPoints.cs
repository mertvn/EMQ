using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Server.Db.Imports;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;
using NUnit.Framework;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Tests;

public class EntryPoints
{
    [Test, Explicit]
    public async Task GenerateAutocompleteMstJson()
    {
        await File.WriteAllTextAsync("autocomplete_mst.json", await DbManager.SelectAutocompleteMst());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteCJson()
    {
        await File.WriteAllTextAsync("autocomplete_c.json", await DbManager.SelectAutocompleteC());
    }

    [Test, Explicit]
    public async Task GenerateAutocompleteAJson()
    {
        await File.WriteAllTextAsync("autocomplete_a.json", await DbManager.SelectAutocompleteA());
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
        var rqIds = Enumerable.Range(5, 20).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Approved);
        }
    }

    [Test, Explicit]
    public async Task RejectReviewQueueItem()
    {
        var rqIds = Enumerable.Range(80, 1).ToArray();

        foreach (int rqId in rqIds)
        {
            await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Rejected);
        }
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
