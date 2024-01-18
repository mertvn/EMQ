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
using NUnit.Framework;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class EntryPoints_Encoding
{
    [Test, Explicit]
    public async Task FindAndEncodeVideos()
    {
        string audioEncoderName = "libvorbis";
        string[] copiableAudioFormats = { "vorbis", "opus" };

        string inputDir = @"M:\!emqraw\!auto";
        // inputDir = @"N:\!checkedsorted";

        string baseOutputDir = @"M:\!emqvideos\!auto";

        // wmapro sources have clicks when converted to ogg or opus for some reason
        string[] blacklistedAudioFormats = { "wmapro" };
        string[] searchForVideoExtensions = { "mpg", "wmv", "avi", "mp4" };

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

                if (result.Width > 1280 || result.Height > 768)
                {
                    // todo
                    throw new NotImplementedException(
                        "resolutions over 1280x720 and 1024x768 are currently not supported");
                }

                if (blacklistedAudioFormats.Contains(result.PrimaryAudioStreamCodecName))
                {
                    Console.WriteLine("skipping blacklisted audio codec");
                    continue;
                }

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
                                $"-g 150 " +
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
            }
        }
    }
}
