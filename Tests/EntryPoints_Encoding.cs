using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;
using NUnit.Framework;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class EntryPoints_Encoding
{
    [Test, Explicit]
    public async Task FindAndEncodeVideos()
    {
        string audioEncoderName = "libopus";
        string[] copiableAudioFormats = { "vorbis", "opus" };

        string inputDir = @"M:\!emqraw\!auto";
        // inputDir = @"N:\!checkedsorted";

        string baseOutputDir = @"M:\!emqvideos\!auto";

        // wmapro sources have clicks when converted to ogg or opus for some reason
        string[] blacklistedAudioFormats = { "wmapro" };
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4", "ogv", "webm" };

        var filePaths = new List<string>();
        foreach (string extension in searchForVideoExtensions)
        {
            filePaths.AddRange(Directory.EnumerateFiles(inputDir, $"*.{extension}",
                new EnumerationOptions { RecurseSubdirectories = true }));
        }

        foreach (string filePath in filePaths)
        {
            // todo? make this configurable
            // todo? just do full inputDir replace
            string oneUnderInputDir = filePath.Replace($"{inputDir}\\", "").Split("\\")[0];
            string outputDir = $"{baseOutputDir}\\{oneUnderInputDir}";
            string outputFinal = $"{outputDir}\\{Path.GetFileNameWithoutExtension(filePath)}.webm";

            try
            {
                if (File.Exists(outputFinal))
                {
                    Console.WriteLine($"skipping existing file {filePath}");
                    continue;
                }

                Console.WriteLine($"processing {filePath}");
                Directory.CreateDirectory(outputDir);
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(60));

                var result = await MediaAnalyser.Analyse(filePath);
                Console.WriteLine(JsonSerializer.Serialize(result, Utils.JsoIndented));

                if (blacklistedAudioFormats.Contains(result.PrimaryAudioStreamCodecName))
                {
                    Console.WriteLine("skipping blacklisted audio codec");
                    continue;
                }

                bool requiresDownscale = result.Width > 1280 || result.Height > 768;
                bool canCopyAudio = copiableAudioFormats.Contains(result.PrimaryAudioStreamCodecName);
                bool encodeAudioSeparately = !canCopyAudio && false;

                float volumeAdjust = MediaAnalyser.GetVolumeAdjust(result);
                (string ss, string to) = await MediaAnalyser.GetSsAndTo(filePath, cancellationTokenSource.Token);

                if (encodeAudioSeparately)
                {
                    // todo
                    throw new NotImplementedException();
                }
                else
                {
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "ffmpeg",
                            Arguments =
                                $"-i \"{filePath}\" " +
                                $"-ss {ss} " +
                                (to.Any() ? $"-to {to} " : "") +
                                $"-map 0:v " +
                                $"-map 0:a " +
                                $"-shortest " +
                                $"-c:v libvpx-vp9 -b:v 2500k -crf 28 -pix_fmt yuv420p " +
                                $"-deadline good -cpu-used 3 -tile-columns 2 -threads 4 -row-mt 1 " +
                                $"-g 100 " +
                                (requiresDownscale ? "-vf \"scale=-1:720,setsar=1\" " : "") +
                                $"-c:a {audioEncoderName} -b:a 320k -ac 2 -af \"volume={volumeAdjust.ToString(CultureInfo.InvariantCulture)}dB\" " +
                                $"-nostdin " +
                                $"\"{outputFinal}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();

                    string err = await process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);
                    if (err.Any())
                    {
                        Console.WriteLine(err);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                if (File.Exists(outputFinal))
                {
                    File.Delete(outputFinal);
                }

                if (Directory.Exists(outputDir) &&
                    Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories).Length == 0)
                {
                    Directory.Delete(outputDir);
                }
            }
        }
    }

    [Test, Explicit]
    public async Task SearchArchives()
    {
        string inputDir = @"N:\!checkedsorted";
        string[] files = Directory.GetFiles(inputDir, "*.rar", SearchOption.AllDirectories); // todo
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4" };
        // string[] searchForVideoStr = { "video", "movie", "mov", "mv" };

        string[] searchForXp3VideoStr = { "video", "movie", "mov", "mv", "op", "ed", "data" };

        string extractToDir = @"M:\!!temp";
        Directory.CreateDirectory(extractToDir);

        foreach (string file in files)
        {
            Console.WriteLine($"processing: {file}");
            try
            {
                var archive = ArchiveFactory.Open(file);
                foreach (IArchiveEntry entry in archive.Entries)
                {
                    if (!entry.Key.EndsWith("dll") &&
                        (searchForVideoExtensions.Any(x => entry.Key.EndsWith(x, StringComparison.OrdinalIgnoreCase))
                            // || searchForVideoStr.Any(x => entry.Key.Contains(x, StringComparison.OrdinalIgnoreCase))
                        ))
                    {
                        // Console.WriteLine(entry.Size);
                        if (entry.Size <= (7 * 1000 * 1000)) // 7 MB
                        {
                            Console.WriteLine($"skipping because of filesize: {entry.Key}");
                            continue;
                        }

                        Console.WriteLine($"extracting: {entry.Key}");

                        string finalDir = Path.Combine(extractToDir, new DirectoryInfo(file).Parent!.Name);
                        Directory.CreateDirectory(finalDir);

                        entry.WriteToDirectory(finalDir,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = false });
                    }

                    if (searchForXp3VideoStr.Any(x =>
                            entry.Key.EndsWith(".xp3", StringComparison.OrdinalIgnoreCase) &&
                            (entry.Key.EndsWith($"{x}.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}1.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}2.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}3.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}4.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}5.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}6.xp3", StringComparison.OrdinalIgnoreCase) ||
                             entry.Key.EndsWith($"{x}7.xp3", StringComparison.OrdinalIgnoreCase))))
                    {
                        Console.WriteLine($"XP3VideoStr: {entry.Key}");
                        string finalDir = Path.Combine(extractToDir, "!xp3", new DirectoryInfo(file).Parent!.Name);
                        // Console.WriteLine(finalDir);

                        Directory.CreateDirectory(finalDir);
                        entry.WriteToDirectory(finalDir,
                            new ExtractionOptions { ExtractFullPath = true, Overwrite = false });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing {file}: {e.Message}");
            }
        }
    }

    [Test, Explicit]
    public async Task GARbro_CLI()
    {
        string inputDir = @"M:\!!temp\!xp3";
        string[] files = Directory.GetFiles(inputDir, "*.xp3", SearchOption.AllDirectories); // todo

        string extractToDir = @"M:\!!temp\!garbrocli";
        Directory.CreateDirectory(extractToDir);

        foreach (string file in files)
        {
            Console.WriteLine($"processing {file}");
            string finalDir = Path.Combine(extractToDir, new DirectoryInfo(file).Parent!.Name);
            // Console.WriteLine(finalDir);

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = @"G:\Code\garbro-crskycode\GARbro\bin\Release\GARbro.Console.exe",
                    Arguments = $"x -ni -ns -na -y -o \"{finalDir}\" \"{file}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            string err = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (err.Any())
            {
                Console.WriteLine(err);
            }

            if (!Directory.Exists(finalDir))
            {
                // can enter here if no files could be extracted from the archive
                continue;
            }

            string[] filesFinal = Directory.GetFiles(finalDir, "*", SearchOption.AllDirectories);
            foreach (string fileFinal in filesFinal)
            {
                bool isValidVideoFile = true;
                try
                {
                    Console.WriteLine($"analyzing {fileFinal}");
                    IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(fileFinal);
                    // todo additional checks?
                    Console.WriteLine($"duration: {mediaInfo.Duration.TotalSeconds}s");
                    if (mediaInfo.Duration > TimeSpan.FromSeconds(3600))
                    {
                        Console.WriteLine("too long");
                        isValidVideoFile = false;
                    }
                    else if (mediaInfo.Duration > TimeSpan.FromSeconds(1) &&
                             mediaInfo.Duration < TimeSpan.FromSeconds(25))
                    {
                        Console.WriteLine("too short 1-25");
                        isValidVideoFile = false;
                    }
                    else if (mediaInfo.Duration <= TimeSpan.FromSeconds(1))
                    {
                        Console.WriteLine("too short <= 1");
                        isValidVideoFile = false;
                    }

                    if (fileFinal.Contains("demo", StringComparison.OrdinalIgnoreCase) || fileFinal.Contains("デモ"))
                    {
                        Console.WriteLine("demo");
                        isValidVideoFile = false;
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("Invalid data found when processing input") ||
                        e.Message.Contains("End of file") ||
                        e.Message.Contains("Invalid frame size") ||
                        e.Message.Contains("Not yet implemented") ||
                        e.Message.Contains("Failed to read frame"))
                    {
                        Console.WriteLine(e.Message);
                        isValidVideoFile = false;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (!isValidVideoFile)
                {
                    File.Delete(fileFinal);
                    if (Directory.GetFiles(finalDir, "*", SearchOption.AllDirectories).Length == 0)
                    {
                        Directory.Delete(finalDir, true);
                    }
                }
            }
        }
    }
}
