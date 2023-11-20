using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
            if (true || !currentDir.Any(x =>
                    x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".ape", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".tak", StringComparison.OrdinalIgnoreCase) ||
                    x.EndsWith(".tta", StringComparison.OrdinalIgnoreCase)
                ))
            {
                bool found = false;
                var flacPaths = currentDir.ToList()
                    .FindAll(x =>
                        x.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".ape", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".tak", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".tta", StringComparison.OrdinalIgnoreCase)
                    );
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
        var fileRegex = new Regex("FILE \"(.+)\" WAVE",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var discRegex = new Regex(@"(?:.+)??(?:dis[ck])(?:[ _-])?([0-9]+)(?:.+)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        List<string> invalidFormatExceptionPaths = new();
        List<string> splitFilesPaths = new();

        var dict = new ConcurrentDictionary<string, string>(); // todo string, info class
        string inputDir = "L:\\olil355 - Copy";
        var filePaths = Directory.GetFiles(inputDir, $"*.cue", SearchOption.AllDirectories).OrderBy(x => x);
        await Parallel.ForEachAsync(filePaths,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 2 }, async (cueFilePath, _) =>
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

                var noSplitFiles = false;
                var currentDir = Directory.GetFiles(Path.GetDirectoryName(cueFilePath)!);
                var flacPaths = currentDir.ToList()
                    .FindAll(x =>
                        x.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".ape", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".tak", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".tta", StringComparison.OrdinalIgnoreCase)
                    );
                if (flacPaths.Any())
                {
                    foreach (string flacPath in flacPaths)
                    {
                        long length = new FileInfo(flacPath).Length;
                        long meb = length / (1024 * 1024);
                        if (meb > 50)
                        {
                            noSplitFiles = true;
                            break;
                        }
                    }
                }
                else
                {
                    // can't be split files if there aren't any sound files next to the cue file at all
                    noSplitFiles = true;
                }

                if (!noSplitFiles)
                {
                    splitFilesPaths.Add(cueFilePath);
                    // Console.WriteLine($"skipping split files dir: {cueFilePath}");
                    return;
                }

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

                var cue = (await File.ReadAllLinesAsync(cueFilePath, _)).ToList();
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
                    bool replaced = false;
                    string[] allCuesInPath = Directory.GetFiles(dirName, $"*.cue", SearchOption.AllDirectories);
                    if (allCuesInPath.Length == 1)
                    {
                        string replaceTak =
                            containerFilePath.Replace(".wav", ".tak", StringComparison.OrdinalIgnoreCase);
                        string replaceTta =
                            containerFilePath.Replace(".wav", ".tta", StringComparison.OrdinalIgnoreCase);
                        string replaceApe =
                            containerFilePath.Replace(".wav", ".ape", StringComparison.OrdinalIgnoreCase);
                        string replaceFlac =
                            containerFilePath.Replace(".wav", ".flac", StringComparison.OrdinalIgnoreCase);
                        if (File.Exists(replaceTak))
                        {
                            Console.WriteLine("foundreplacement .tak");
                            File.Copy(cueFilePath, cueFilePath + ".bak.wav2tak");

                            string toReplace = containerFilePath.Split("\\").Last();
                            string replaceWith = replaceTak.Split("\\").Last();
                            var contents = cue.Select(x => x.Replace(toReplace, replaceWith));
                            await File.WriteAllLinesAsync(cueFilePath, contents, _);

                            containerFilePath = replaceTak;
                            replaced = true;
                        }
                        else if (File.Exists(replaceTta))
                        {
                            Console.WriteLine("foundreplacement .tta");
                            File.Copy(cueFilePath, cueFilePath + ".bak.wav2tta");

                            string toReplace = containerFilePath.Split("\\").Last();
                            string replaceWith = replaceTta.Split("\\").Last();
                            var contents = cue.Select(x => x.Replace(toReplace, replaceWith));
                            await File.WriteAllLinesAsync(cueFilePath, contents, _);

                            containerFilePath = replaceTta;
                            replaced = true;
                        }
                        else if (File.Exists(replaceApe))
                        {
                            Console.WriteLine("foundreplacement .ape");
                            File.Copy(cueFilePath, cueFilePath + ".bak.wav2ape");

                            string toReplace = containerFilePath.Split("\\").Last();
                            string replaceWith = replaceApe.Split("\\").Last();
                            var contents = cue.Select(x => x.Replace(toReplace, replaceWith));
                            await File.WriteAllLinesAsync(cueFilePath, contents, _);

                            containerFilePath = replaceApe;
                            replaced = true;
                        }
                        else if (File.Exists(replaceFlac))
                        {
                            Console.WriteLine("foundreplacement .flac");
                            File.Copy(cueFilePath, cueFilePath + ".bak.wav2flac");

                            string toReplace = containerFilePath.Split("\\").Last();
                            string replaceWith = replaceFlac.Split("\\").Last();
                            var contents = cue.Select(x => x.Replace(toReplace, replaceWith));
                            await File.WriteAllLinesAsync(cueFilePath, contents, _);

                            containerFilePath = replaceFlac;
                            replaced = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("allCuesInPath.Length: " + allCuesInPath.Length);
                    }

                    if (!replaced)
                    {
                        Console.WriteLine("containerFile not found");
                        return;
                    }
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

                // if (cueFilePath.Contains("この青空に約束を― 初回特典 オリジナルサウンドトラック Disc1"))
                // {
                //     Console.WriteLine();
                // }

                int discNumber = 0;
                string cueFileName = Path.GetFileNameWithoutExtension(cueFilePath);
                string parentDirectoryName = new DirectoryInfo(cueFilePath).Parent!.Name;
                var matchCue = discRegex.Match(cueFileName);
                if (matchCue.Success)
                {
                    discNumber = Convert.ToInt32(matchCue.Groups[1].Value);
                }
                else
                {
                    var matchParentDirectory = discRegex.Match(parentDirectoryName);
                    if (matchParentDirectory.Success)
                    {
                        discNumber = Convert.ToInt32(matchParentDirectory.Groups[1].Value);
                    }
                }

                Console.WriteLine($"discNumber: {discNumber}");
                // return;

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
                        // continue;
                    }

                    // string outputDir = "L:\\!tracks";
                    // var od = cueFilePath.Replace(dir, outputDir).Replace(".cue", "");
                    var od = $"{dirName}\\Disuku{discNumber}";
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
                                    .WithTagVersion(3)
                                    .WithCustomArgument($"-metadata artist=\"{artist}\"")
                                    .WithCustomArgument($"-metadata title=\"{title}\"")
                                    .WithCustomArgument($"-metadata album=\"{album}\"")
                                    .WithCustomArgument($"-metadata track=\"{trackNumber}\"")
                                    .WithCustomArgument($"-metadata discnumber=\"{discNumber}\"")
                                    .UsingThreads(1)
                                    .WithCustomArgument("-nostdin")
                                ).ProcessAsynchronously();
                        }
                        catch (Exception)
                        {
                            if (isInvalidFormat)
                            {
                                invalidFormatExceptionPaths.Add(containerFilePath);
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

        await File.WriteAllTextAsync(
            $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}/isInvalidFormat.json",
            JsonSerializer.Serialize(invalidFormatExceptionPaths, Utils.JsoIndented));
        foreach (string invalidFormatExceptionPath in invalidFormatExceptionPaths)
        {
            Console.WriteLine($"isInvalidFormat exception for: {invalidFormatExceptionPath}");
        }

        await File.WriteAllTextAsync(
            $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}/splitFiles.json",
            JsonSerializer.Serialize(splitFilesPaths, Utils.JsoIndented));
        foreach (string splitFilesPath in splitFilesPaths)
        {
            Console.WriteLine($"splitFiles exception for: {splitFilesPath}");
        }
    }
}
