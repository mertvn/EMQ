using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

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
                string filePath = System.IO.Path.GetTempPath() + rq.url.LastSegment();

                bool dlSuccess =
                    await ExtensionMethods.DownloadFile(
                        new HttpClient
                        {
                            DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("g", "4") } },
                            Timeout = TimeSpan.FromMinutes(30)
                        },
                        filePath, new Uri(rq.url));
                if (dlSuccess)
                {
                    var analyserResult = await MediaAnalyser.Analyse(filePath);
                    System.IO.File.Delete(filePath);

                    await DbManager.UpdateReviewQueueItem(rq.id, ReviewQueueStatus.Pending,
                        analyserResult: analyserResult);
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
}
