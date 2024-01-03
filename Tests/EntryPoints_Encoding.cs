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
using NUnit.Framework;

namespace Tests;

// ReSharper disable once InconsistentNaming
public class EntryPoints_Encoding
{
    [Test, Explicit]
    public async Task FindAndEncodeVideos()
    {
        float targetVolumeMean = -15.6f;
        float targetVolumeMax = -0.6f;
        float silenceLeewaySeconds = 0.2f;
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

                float meanVolume = Convert.ToSingle(result.VolumeDetect?.Single(x => x.Contains("mean_volume"))
                    .Replace("mean_volume:", "")
                    .Replace("dB", "")
                    .Trim(), CultureInfo.InvariantCulture);

                float maxVolume = Convert.ToSingle(result.VolumeDetect?.Single(x => x.Contains("max_volume"))
                    .Replace("max_volume:", "")
                    .Replace("dB", "")
                    .Trim(), CultureInfo.InvariantCulture);

                if (meanVolume == 0)
                {
                    throw new Exception("meanVolume is 0");
                }

                float volumeAdjust = 0;
                if (meanVolume > targetVolumeMean)
                {
                    volumeAdjust = targetVolumeMean - meanVolume;
                }
                else if (maxVolume > targetVolumeMax)
                {
                    volumeAdjust = targetVolumeMax - maxVolume;
                }

                Console.WriteLine($"volumeAdjust: {volumeAdjust}");
                string ss = TimeSpan.FromSeconds(0).ToString("c");
                string to = "";

                {
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i \"{filePath}\" -map a:0 -af silencedetect=n=-55dB:d=0.5 -f null -",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process.Start();
                    string err = await process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);
                    if (err.Any())
                    {
                        string[] lines = err.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                        string[] silencedetectLines = lines.Where(x => x.Contains("silence_")).ToArray();

                        string[] final = new string[silencedetectLines.Length];
                        for (int index = 0; index < silencedetectLines.Length; index++)
                        {
                            string volumedetectLine = silencedetectLines[index].Trim();
                            final[index] = new string(volumedetectLine.SkipWhile(c => c != ']').ToArray()[1..]);
                        }

                        Console.WriteLine(JsonSerializer.Serialize(final, Utils.Jso));
                        switch (final.Length)
                        {
                            case 0: // no silence
                                break;
                            case 2: // start or end silence
                                {
                                    float silenceStart = Convert.ToSingle(
                                        final[0].Replace("silence_start: ", "").Trim(),
                                        CultureInfo.InvariantCulture);

                                    string[] split = final[1].Split('|');
                                    float silenceEnd = Convert.ToSingle(
                                        split[0].Replace("silence_end: ", "").Trim(),
                                        CultureInfo.InvariantCulture);

                                    if (silenceStart <= 0)
                                    {
                                        // start silence
                                        ss = TimeSpan.FromSeconds(silenceEnd - silenceLeewaySeconds).ToString("c");
                                    }
                                    else
                                    {
                                        // end silence
                                        to = TimeSpan.FromSeconds(silenceStart + silenceLeewaySeconds).ToString("c");
                                    }

                                    break;
                                }
                            case 4: // start and end silence
                                {
                                    {
                                        float silenceStart = Convert.ToSingle(
                                            final[0].Replace("silence_start: ", "").Trim(),
                                            CultureInfo.InvariantCulture);

                                        string[] split = final[1].Split('|');
                                        float silenceEnd = Convert.ToSingle(
                                            split[0].Replace("silence_end: ", "").Trim(),
                                            CultureInfo.InvariantCulture);

                                        // need a little bit of tolerance here
                                        if (silenceStart > 0.2f)
                                        {
                                            throw new Exception("case 4 silence_start not 0");
                                        }

                                        ss = TimeSpan.FromSeconds(silenceEnd - silenceLeewaySeconds).ToString("c");
                                    }

                                    {
                                        float silenceStart = Convert.ToSingle(
                                            final[2].Replace("silence_start: ", "").Trim(),
                                            CultureInfo.InvariantCulture);

                                        if (silenceStart <= 0)
                                        {
                                            throw new Exception("case 4 silence_start 0");
                                        }

                                        // end silence
                                        to = TimeSpan.FromSeconds(silenceStart + silenceLeewaySeconds).ToString("c");
                                    }

                                    break;
                                }
                            case 1:
                            case 3:
                                {
                                    throw new Exception("found unmatched silence");
                                }
                            default:
                                {
                                    // we only allow start and end silence for automatic processing
                                    throw new Exception("found more than two instances of silence");
                                }
                        }
                    }
                }

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
                                $"-c:a {audioEncoderName} -b:a 192k -ac 2 -af \"volume={volumeAdjust.ToString(CultureInfo.InvariantCulture)}dB\" " +
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
