using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Shared.Core;

namespace EMQ.Server.Db.Imports.SongMatching.GGVC;

public static class GGVCImporter
{
    public static async Task DeleteAlreadyImportedGGVCFiles()
    {
        var uploaded2 =
            JsonSerializer.Deserialize<List<Uploadable>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc2\\uploaded.json"),
                Utils.JsoIndented)!;

        var uploaded3 =
            JsonSerializer.Deserialize<List<Uploadable>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc3\\uploaded.json"),
                Utils.JsoIndented)!;

        // todo add alreadyHave to this
        var uploaded = uploaded2.Concat(uploaded3).ToList();

        string output = "@echo off\r\n";
        foreach (Uploadable uploadable in uploaded)
        {
            string path = uploadable.Path.Replace("\\\\", "\\")
                .Replace(@"M:\[IMS][Galgame Vocal MP3 Collection 1996-2006]\", "");
            output += $"del \"{path}\"\r\n";
        }

        await File.WriteAllTextAsync("ggvc_delete_uploaded.bat", output);
    }

    public static async Task ImportGGVC()
    {
        var regex = new Regex("【(.+)】(?: )?(.+)(?: )?(?:\\[|【)(.*)(?:]|】)", RegexOptions.Compiled);
        var songMatches = new List<SongMatch>();

        string dir = "M:\\[IMS][Galgame Vocal MP3 Collection 1996-2006]";
        // dir = "M:\\a";
        string[] subDirs = Directory.GetDirectories(dir);
        foreach (string subDir in subDirs)
        {
            // Console.WriteLine("start subDir " + subDir);
            string[] filePaths = Directory.GetFiles(subDir);
            foreach (string filePath in filePaths)
            {
                string fileName = Path.GetFileName(filePath);
                if (!fileName.StartsWith("【") ||
                    fileName.ToLowerInvariant().Contains("mix") ||
                    fileName.ToLowerInvariant().Contains("ｍｉｘ") ||
                    fileName.ToLowerInvariant().Contains("radioedit") ||
                    fileName.ToLowerInvariant().Contains("arrang") ||
                    fileName.ToLowerInvariant().Contains("アレンジ") ||
                    fileName.ToLowerInvariant().Contains("acoustic") ||
                    fileName.ToLowerInvariant().Contains("裏ver") ||
                    (fileName.ToLowerInvariant().Contains("ver.") &&
                     !fileName.ToLowerInvariant().EndsWith("forever.mp3")))
                {
                    Console.WriteLine("skipping: " + fileName);
                    continue;
                }

                var match = regex.Match(fileName);
                // Console.WriteLine("match: " + match.ToString());

                if (string.IsNullOrWhiteSpace(match.ToString()))
                {
                    Console.WriteLine("regex didn't match: " + fileName);
                    // throw new Exception("didn't match: " + fileName);
                }

                string source = match.Groups[1].Value;
                string title = match.Groups[2].Value;
                string artist = match.Groups[3].Value;

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(title) ||
                    string.IsNullOrWhiteSpace(artist))
                {
                    continue;
                }

                source = source.Trim();
                title = title.Trim();
                artist = artist.Trim();

                List<string> sources = new() { source };
                List<string> titles = new() { title };
                List<string> artists = new() { artist };

                var tFile = TagLib.File.Create(filePath);
                string? metadataSources = tFile.Tag.Album;
                string? metadataTitle = tFile.Tag.Title;
                string[] metadataArtists = tFile.Tag.Performers.Concat(tFile.Tag.AlbumArtists).ToArray();

                if (!string.IsNullOrWhiteSpace(metadataSources))
                {
                    sources.Add(metadataSources);
                }

                if (!string.IsNullOrWhiteSpace(metadataTitle))
                {
                    titles.Add(metadataTitle);
                }

                artists.AddRange(metadataArtists);

                sources = sources.Distinct().ToList();
                titles = titles.Distinct().ToList();
                artists = artists.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

                var songMatch = new SongMatch
                {
                    Path = filePath, Sources = sources, Titles = titles, Artists = artists,
                };
                songMatches.Add(songMatch);
            }
        }

        await SongMatcher.Match(songMatches, "C:\\emq\\ggvc4");
    }
}
