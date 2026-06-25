using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EMQ.Shared.Core;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace EMQ.Server;

public sealed class ImageFetcherService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        return;
#endif

        if (!Constants.IsUra)
        {
            return;
        }

        Console.WriteLine("ImageFetcherService is starting");
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(360));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWork();
        }
    }

    private static async Task DoWork()
    {
        try
        {
            var haveMsIds = new List<int>();
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            var msIds =
                (await connection.QueryAsync<(int, string)>(
                    @"select ms.id, msel.url from music_source ms JOIN music_source_external_link msel ON msel.music_source_id = ms.id where ms.type = 70 and msel.type = 10"))
                .ToDictionary(x => x.Item1, x => x.Item2);
            if (!msIds.Any())
            {
                return;
            }

            var connectionInfo =
                new Renci.SshNet.ConnectionInfo(UploadConstants.SftpHost, UploadConstants.SftpUsername,
                    new PasswordAuthenticationMethod(UploadConstants.SftpUsername, UploadConstants.SftpPassword));
            using var client = new SftpClient(connectionInfo);
            client.Connect();

            string mbImgDir = $"{UploadConstants.SftpUserUploadDir.Replace("userup/", "")}mb-img/";
            var files = GetAllFilesRecursively(client, mbImgDir);
            foreach (ISftpFile file in files)
            {
                if (int.TryParse(file.Name, out int i))
                {
                    haveMsIds.Add(i);
                }
            }

            var missingMsIds = msIds.Keys.Except(haveMsIds);
            foreach (int missingMsId in missingMsIds)
            {
                string mbid = msIds[missingMsId].Replace("https://musicbrainz.org/release-group", "").Replace("/", "");
                var res = await ServerUtils.Client.GetAsync(
                    $"https://coverartarchive.org/release-group/{mbid}/front-500");
                if (res.IsSuccessStatusCode)
                {
                    byte[] content = await res.Content.ReadAsByteArrayAsync();
                    (string? modStr, int number) = Utils.ParseVndbScreenshotStr($"cv{missingMsId}");
                    string modDir = $"{mbImgDir}cv/{modStr}/";
                    if (!client.Exists(modDir))
                    {
                        client.CreateDirectory(modDir);
                    }

                    client.WriteAllBytes($"{modDir}{number}.jpg", content);
                }
            }

            client.Disconnect();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static List<ISftpFile> GetAllFilesRecursively(SftpClient client, string remotePath)
    {
        var allFiles = new List<ISftpFile>();
        var items = client.ListDirectory(remotePath);
        foreach (var item in items)
        {
            if (item.Name is "." or "..")
                continue;

            if (item.IsDirectory)
            {
                var subFiles = GetAllFilesRecursively(client, item.FullName);
                allFiles.AddRange(subFiles);
            }
            else
            {
                allFiles.Add(item);
            }
        }

        return allFiles;
    }
}
