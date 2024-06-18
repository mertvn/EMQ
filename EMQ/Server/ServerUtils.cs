using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Renci.SshNet;
using Session = EMQ.Shared.Auth.Entities.Concrete.Session;

namespace EMQ.Server;

public static class ServerUtils
{
    public static HttpClient Client { get; } =
        new(new HttpClientHandler
        {
            UseProxy = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development"
        }) { DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("EMQ", "7.8") } } };

    // public static IConfiguration AppSettings { get; } = new ConfigurationBuilder()
    //     .AddJsonFile(Constants.IsDevelopmentEnvironment ? "appsettings.Development.json" : "appsettings.json",
    //         false, true).Build();

    public static void RunAggressiveGc()
    {
        // Console.WriteLine("Running GC");
        // long before = GC.GetTotalMemory(false);

        // yes, we really need to do this twice
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        // long after = GC.GetTotalMemory(false);
        // Console.WriteLine($"GC freed {(before - after) / 1000 / 1000} MB");
    }

    public static async Task RunAnalysis()
    {
        var rqs = await DbManager.FindRQs(DateTime.MinValue, DateTime.MaxValue);
        foreach (RQ rq in rqs)
        {
            if (rq.analysis == "Pending")
            {
                string filePath = Path.GetTempPath() + rq.url.LastSegment();
                bool dlSuccess = await Client.DownloadFile(filePath, new Uri(rq.url));
                if (dlSuccess)
                {
                    bool? isVideoOverride = null;
                    if (filePath.EndsWith(".weba"))
                    {
                        isVideoOverride = false;
                    }

                    var analyserResult = await MediaAnalyser.Analyse(filePath, false, isVideoOverride);
                    File.Delete(filePath);

                    await DbManager.UpdateReviewQueueItem(rq.id, ReviewQueueStatus.Pending,
                        analyserResult: analyserResult);
                }
            }
        }

        int end = await DbManager.SelectCountUnsafe("music");
        var songs = await DbManager.SelectSongsMIds(Enumerable.Range(1, end).ToArray(), false);

        foreach (Song song in songs)
        {
            foreach (SongLink songLink in song.Links)
            {
                string tempPath = $"{Path.GetTempPath()}/{songLink.Url.LastSegment()}";
                try
                {
                    bool success = await Client.DownloadFile(tempPath, new Uri(songLink.Url));
                    if (success)
                    {
                        var analyserResult = await MediaAnalyser.Analyse(tempPath);
                        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                        int rows = await connection.ExecuteAsync(
                            "UPDATE music_external_link SET analysis_raw = @analyserResult WHERE url = @url",
                            new { analyserResult, url = songLink.Url.UnReplaceSelfhostLink() });

                        if (rows <= 0)
                        {
                            Console.WriteLine($"failed to set analysis_raw: {songLink.Url}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"failed to download file: {songLink.Url}");
                    }
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
        }
    }

    public static string? GetIpAddress(HttpContext context)
    {
        string? ip = context.Connection.RemoteIpAddress?.ToString();
        string? header = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ??
                         context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (IPAddress.TryParse(header, out IPAddress? i))
        {
            ip = i.ToString();
        }

        return ip;
    }

    // todo move all of these
    public static async Task<List<PlayerVndbInfo>> GetAllVndbInfos(List<Session> sessions)
    {
        var ret = new List<PlayerVndbInfo>();
        foreach (Session session in sessions)
        {
            PlayerVndbInfo vndbInfo =
                await GetVndbInfo_Inner(session.Player.Id, session.ActiveUserLabelPresetName);
            ret.Add(vndbInfo);
        }

        return ret;
    }

    public static Label FromUserLabel(UserLabel userLabel)
    {
        var label = new Label()
        {
            Id = userLabel.vndb_label_id,
            IsPrivate = userLabel.vndb_label_is_private,
            Name = userLabel.vndb_label_name,
            Kind = (LabelKind)userLabel.kind,
        };
        return label;
    }

    // todo return null if not found
    public static async Task<PlayerVndbInfo> GetVndbInfo_Inner(int userId, string? presetName)
    {
        // var stopWatch = new Stopwatch();
        // stopWatch.Start();

        var vndbInfo = await DbManager.GetUserVndbInfo(userId, presetName);
        if (!string.IsNullOrWhiteSpace(vndbInfo.VndbId) && !string.IsNullOrEmpty(presetName))
        {
            var userLabels = await DbManager.GetUserLabels(userId, vndbInfo.VndbId, presetName);
            vndbInfo.Labels = new List<Label>();

            // todo batch
            // todo? method
            foreach (UserLabel userLabel in userLabels)
            {
                var label = FromUserLabel(userLabel);
                var userLabelVns = await DbManager.GetUserLabelVns(userLabel.id);
                foreach (UserLabelVn userLabelVn in userLabelVns)
                {
                    label.VNs[userLabelVn.vnid] = userLabelVn.vote;
                }

                vndbInfo.Labels.Add(label);
            }
        }

        if (vndbInfo.Labels != null)
        {
            // default labels (Id <= 7) are always first, and then the custom labels appear in an alphabetically-sorted order
            // this implementation doesn't work correctly if the user has labels named 0-9, but meh
            vndbInfo.Labels = vndbInfo.Labels.OrderBy(x => x.Id <= 7 ? x.Id.ToString() : x.Name).ToList();
        }

        // stopWatch.Stop();
        // Console.WriteLine(
        //     $"GetVndbInfo_Inner took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        return vndbInfo;
    }

    public static void SftpFileUpload(string ftpUrl, string username, string password, Stream stream, string remotePath)
    {
        if (stream.Length == 0)
        {
            throw new Exception("stream length is 0");
        }

        var connectionInfo =
            new Renci.SshNet.ConnectionInfo(ftpUrl, username, new PasswordAuthenticationMethod(username, password));
        using (var client = new SftpClient(connectionInfo))
        {
            client.Connect();
            Console.WriteLine(client.ConnectionInfo.CurrentClientEncryption);
            Console.WriteLine(client.ConnectionInfo.CurrentServerEncryption);
            client.UploadFile(stream, remotePath);
            client.Disconnect();
        }
    }

    public static async Task<MediaAnalyserResult?> ImportSongLinkInner(int mId, SongLink songLink, string existingPath,
        bool? isVideoOverride)
    {
        int rqId = await DbManager.InsertReviewQueue(mId, songLink, "Pending");
        MediaAnalyserResult? analyserResult = null;

        if (rqId > 0)
        {
            if (!string.IsNullOrEmpty(existingPath))
            {
                analyserResult = await MediaAnalyser.Analyse(existingPath, isVideoOverride: isVideoOverride);
                await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                    analyserResult: analyserResult);
            }
            else
            {
                string filePath = System.IO.Path.GetTempPath() + songLink.Url.LastSegment();
                bool dlSuccess = await ServerUtils.Client.DownloadFile(filePath, new Uri(songLink.Url));
                if (dlSuccess)
                {
                    analyserResult = await MediaAnalyser.Analyse(filePath, isVideoOverride: isVideoOverride);
                    System.IO.File.Delete(filePath);
                    await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                        analyserResult: analyserResult);
                }
            }
        }

        return analyserResult;
    }

    public static async Task<(MediaAnalyserResult?, int rqId)> ImportSongLinkInnerWithRQId(int mId, SongLink songLink,
        string existingPath,
        bool? isVideoOverride)
    {
        int rqId = await DbManager.InsertReviewQueue(mId, songLink, "Pending");
        MediaAnalyserResult? analyserResult = null;

        if (rqId > 0)
        {
            if (!string.IsNullOrEmpty(existingPath))
            {
                analyserResult = await MediaAnalyser.Analyse(existingPath, isVideoOverride: isVideoOverride);
                await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                    analyserResult: analyserResult);
            }
            else
            {
                string filePath = System.IO.Path.GetTempPath() + songLink.Url.LastSegment();
                bool dlSuccess = await ServerUtils.Client.DownloadFile(filePath, new Uri(songLink.Url));
                if (dlSuccess)
                {
                    analyserResult = await MediaAnalyser.Analyse(filePath, isVideoOverride: isVideoOverride);
                    System.IO.File.Delete(filePath);
                    await DbManager.UpdateReviewQueueItem(rqId, ReviewQueueStatus.Pending,
                        analyserResult: analyserResult);
                }
            }
        }

        return (analyserResult, rqId);
    }
}
