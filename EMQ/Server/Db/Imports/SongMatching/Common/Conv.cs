using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CueSharp;
using EMQ.Shared.Core;
using FFMpegCore;
using FFMpegCore.Enums;
using UtfUnknown;

namespace EMQ.Server.Db.Imports.SongMatching.Common;

public static class Conv
{
    public static async Task FixCUEEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var dir = "L:\\olil355 - Copy";
        // var dir = "L:\\FolderI";
        var filePaths = Directory.GetFiles(dir, $"*.cue", SearchOption.AllDirectories).OrderBy(x => x);

        // var di = new DirectoryInfo(dir);
        // var extensionCounts = di.EnumerateFiles("*.*", SearchOption.AllDirectories)
        //     .GroupBy(x => x.Extension)
        //     .Select(g => new { Extension = g.Key, Count = g.Count() })
        //     .OrderByDescending(g => g.Count)
        //     .ToList();
        //
        // foreach (var group in extensionCounts)
        // {
        //     Console.WriteLine("{1}: {0}", group.Count,
        //         group.Extension);
        // }
        // return;

        foreach (string filePath in filePaths)
        {
            Console.WriteLine("--------------------------------------------------------");
            Console.WriteLine(filePath);
            var currentDir = Directory.GetFiles(Path.GetDirectoryName(filePath)!);
            if (!currentDir.Any(x =>
                    x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".ape", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".tak", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".tta", StringComparison.OrdinalIgnoreCase)
                ))
            {
                bool found = false;
                var flacPaths = currentDir.ToList()
                    .FindAll(x => x.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));
                if (flacPaths.Any())
                {
                    foreach (string flacPath in flacPaths)
                    {
                        long length = new FileInfo(flacPath).Length;
                        long meb = length / (1024 * 1024);
                        if (meb > 50)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    Console.WriteLine("cue target not found");
                    continue;
                }
            }

            DetectionResult result = CharsetDetector.DetectFromFile(filePath);
            Console.WriteLine(result.ToString());

            if (result.Detected == null)
            {
                Console.WriteLine("!!! DETECTED IS NULL !!!");
                result = new DetectionResult(new DetectionDetail("GB18030", 0));
            }

            if (result.Detected.Confidence < 0.5)
            {
                Console.WriteLine("!!! LOW CONFIDENCE !!!");
                result.Detected.Encoding = Encoding.GetEncoding("GB18030");
            }

            if (result.Detected.Encoding.EncodingName != Encoding.UTF8.EncodingName || !result.Detected.HasBOM)
            {
                Console.WriteLine($"Converting from {result.Detected.Encoding.EncodingName} to UTF8");
                // var outputPath = $"L:\\!cue\\{Path.GetFileName(filePath)}";

                if (!File.Exists($"{filePath}.bak"))
                {
                    File.Copy(filePath, $"{filePath}.bak");
                }

                var file = await File.ReadAllTextAsync(filePath, result.Detected.Encoding);
                await File.WriteAllTextAsync(filePath, file, new UTF8Encoding(true));
            }
        }
    }

    public static async Task SplitTracks()
    {
        var fileRegex = new Regex("FILE \"(.+)\" WAVE", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var dict = new ConcurrentDictionary<string, string>(); // todo string, info class

        string inputDir = "L:\\olil355 - Copy";
        var filePaths = Directory.GetFiles(inputDir, $"*.cue", SearchOption.AllDirectories).OrderBy(x => x);
        await Parallel.ForEachAsync(filePaths,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 }, async (cueFilePath, _) =>
            {
                Console.WriteLine("--------------------------------------------------------");
                Console.WriteLine(cueFilePath);

                string dirName = Path.GetDirectoryName(cueFilePath)!;

                // string[] allFilesInDir = Directory.GetFiles(dirName);
                // if (allFilesInDir.Count(x => x.EndsWith(".cue")) > 1)
                // {
                //     Console.WriteLine("Multiple cue files");
                //     // continue;
                // }
                // else
                // {
                //     Console.WriteLine("Single cue file");
                // }

                CueSheet cueSheet;
                try
                {
                    cueSheet = new CueSheet(cueFilePath, Encoding.UTF8);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return;
                }

                var cue = (await File.ReadAllLinesAsync(cueFilePath)).ToList();
                var fileLine = cue.FindAll(x => x.StartsWith("FILE", StringComparison.OrdinalIgnoreCase));
                if (!fileLine.Any())
                {
                    Console.WriteLine("FILE line not found");
                    return;
                }

                if (fileLine.Count > 1)
                {
                    Console.WriteLine("multiple FILE lines");
                    return;
                }

                string containerFileName = fileRegex.Match(fileLine[0]).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(containerFileName))
                {
                    Console.WriteLine("FILE regex didn't match");
                    return;
                }

                string containerFilePath = Path.Join(dirName, containerFileName);
                if (!File.Exists(containerFilePath))
                {
                    Console.WriteLine("containerFile not found");
                    return;
                }

                bool isInvalidFormat = false;
                // apparently ffmpeg can't parse some .tak files
                if (Path.GetExtension(containerFilePath) == ".tak")
                {
                    Console.WriteLine("invalid containerFile format");
                    isInvalidFormat = true;
                    // return;
                }

                if (!dict.TryAdd(cueFilePath, containerFilePath))
                {
                    Console.WriteLine("duplicate");
                    dict.Remove(cueFilePath, out string? _);
                    return;
                }

                Console.WriteLine("<GOOD>");
                // continue;

                var tracks = cueSheet.Tracks;
                for (int index = 0; index < tracks.Length; index++)
                {
                    var track = tracks[index];
                    bool isLastTrack = index == tracks.Length - 1;
                    var index01 = track.Indices.Single(x => x.Number == 1);

                    var start = TimeSpan.FromMilliseconds(
                        index01.Minutes * 60 * 1000 + index01.Seconds * 1000 + index01.Frames * 13.33333333);

                    TimeSpan? duration = null;
                    if (!isLastTrack)
                    {
                        var nextTrack = tracks[index + 1];
                        var nextIndex01 = nextTrack.Indices.Single(x => x.Number == 1);
                        var nextStart = TimeSpan.FromMilliseconds(
                            nextIndex01.Minutes * 60 * 1000 + nextIndex01.Seconds * 1000 +
                            nextIndex01.Frames * 13.33333333);
                        duration = nextStart - start;
                    }

                    string artist = track.Performer;
                    string title = track.Title;
                    string album = cueSheet.Title;
                    int trackNumber = track.TrackNumber;

                    if (string.IsNullOrWhiteSpace(title) ||
                        string.IsNullOrWhiteSpace(artist) ||
                        string.Equals(artist, "Unknown Artist", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(artist, "Unknown", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(artist, "Various Artists", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(artist, "VA", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(artist, "不明なアーティスト", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(artist, "不明", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("invalid metadata");
                        continue;
                    }

                    // string outputDir = "L:\\!tracks";
                    // var od = cueFilePath.Replace(dir, outputDir).Replace(".cue", "");
                    var od = dirName;

                    Directory.CreateDirectory(od);
                    string outputPath =
                        $"{od}\\{trackNumber:000}. {Utils.FixFileName(title)} - ({Utils.FixFileName(artist)}).mp3";

                    if (!File.Exists(outputPath))
                    {
                        Console.WriteLine($"converting to {outputPath}");

                        try
                        {
                            await FFMpegArguments
                                .FromFileInput(containerFilePath)
                                .OutputToFile(outputPath, false, options => options
                                    .Seek(start)
                                    .WithDuration(duration)
                                    .WithAudioCodec(AudioCodec.LibMp3Lame)
                                    .WithAudioBitrate(192)
                                    .WithTagVersion()
                                    .WithCustomArgument($"-metadata artist=\"{artist}\"")
                                    .WithCustomArgument($"-metadata title=\"{title}\"")
                                    .WithCustomArgument($"-metadata album=\"{album}\"")
                                    .WithCustomArgument($"-metadata track=\"{trackNumber}\"")
                                    .UsingThreads(1)
                                    .WithCustomArgument("-nostdin")
                                ).ProcessAsynchronously();
                        }
                        catch (Exception)
                        {
                            if (isInvalidFormat)
                            {
                                return;
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                }
            });
    }
}
