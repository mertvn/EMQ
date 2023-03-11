using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Server.Db.Imports.GGVC;

namespace EMQ.Server.Db.Imports;

public static class ToraImporter
{
    public static async Task ImportTora()
    {
        var regex = new Regex("\\((.+)\\)(.+)().mp3", RegexOptions.Compiled);
        var ggvcSongs = new List<GGVCSong>();

        string dir = "M:\\[サントラ] ゲーム系曲集1-288 [MP3合集]";
        // dir = "M:\\a";
        string[] subDirs1 = Directory.GetDirectories(dir);
        foreach (string subDir1 in subDirs1)
        {
            string[] subDirs = Directory.GetDirectories(subDir1);
            foreach (string subDir in subDirs)
            {
                // Console.WriteLine("start subDir " + subDir);
                string[] filePaths = Directory.GetFiles(subDir);
                foreach (string filePath in filePaths)
                {
                    string fileName = Path.GetFileName(filePath);
                    if (!fileName.StartsWith("(") ||
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

                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(title))
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

                    var ggvcSong = new GGVCSong
                    {
                        Path = filePath, Sources = sources, Titles = titles, Artists = artists,
                    };
                    ggvcSongs.Add(ggvcSong);
                }
            }
        }

        await SongMatcher.Match(ggvcSongs, "C:\\emq\\tora2");
    }
}
