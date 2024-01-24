using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;

namespace EMQ.Server.Business;

public static class MediaAnalyser
{
    public static readonly SemaphoreSlim SemaphoreTranscode = new(UploadConstants.MaxConcurrentTranscodes);

    // todo detect bad transcodes
    public static async Task<MediaAnalyserResult> Analyse(string filePath, bool returnEarlyIfInvalidFormat = false,
        bool? isVideoOverride = null)
    {
        string[] validAudioFormats = { "ogg", "mp3" };
        string[] validVideoFormats = { "mp4", "webm" };

        var result = new MediaAnalyserResult { IsValid = false, Warnings = new List<MediaAnalyserWarningKind>(), };

        try
        {
            Console.WriteLine("Analysing " + filePath);

            await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            result.Sha256 = CryptoUtils.Sha256Hash(fs);
            await fs.DisposeAsync();

            IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(filePath);

            // Console.WriteLine(new { mediaInfo.Duration });
            result.Duration = mediaInfo.Duration;
            if (mediaInfo.Duration < TimeSpan.FromSeconds(25))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooShort);
            }

            if (mediaInfo.Duration > TimeSpan.FromSeconds(900))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooLong);
            }

            result.PrimaryAudioStreamCodecName =
                mediaInfo.PrimaryAudioStream?.CodecName ?? mediaInfo.AudioStreams.First().CodecName;
            // Console.WriteLine(new { mediaInfo.Format.FormatName });
            result.FormatList = mediaInfo.Format.FormatName;
            bool isVideo = true;
            string? format = validAudioFormats.FirstOrDefault(x => mediaInfo.Format.FormatName.Contains(x));
            if (format != null)
            {
                isVideo = false;
            }
            else
            {
                format = validVideoFormats.FirstOrDefault(x => mediaInfo.Format.FormatName.Contains(x));
                if (format != null)
                {
                    isVideo = true;
                }
                else
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.InvalidFormat);
                    if (returnEarlyIfInvalidFormat)
                    {
                        return result;
                    }
                }
            }

            if (isVideoOverride != null)
            {
                isVideo = isVideoOverride.Value;
            }

            result.IsVideo = isVideo;

            // Console.WriteLine(new { format });
            result.FormatSingle = format;
            if (!string.Equals($".{format}", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.WrongExtension);
            }

            long filesizeBytes = new FileInfo(filePath).Length;
            result.FilesizeMb = (float)filesizeBytes / 1024 / 1024;

            if (isVideo)
            {
                // Console.WriteLine(new { mediaInfo.PrimaryVideoStream!.AvgFrameRate });
                result.AvgFramerate = mediaInfo.PrimaryVideoStream!.AvgFrameRate;
                result.Width = mediaInfo.PrimaryVideoStream.Width;
                result.Height = mediaInfo.PrimaryVideoStream.Height;
                result.VideoBitrateKbps = mediaInfo.PrimaryVideoStream.BitRate / 1000;
                result.OverallBitrateKbps = ((filesizeBytes * 8) / result.Duration!.Value.TotalSeconds) / 1000;

                if (result.AvgFramerate is 1000)
                {
                    result.AvgFramerate = mediaInfo.PrimaryVideoStream!.FrameRate;
                }

                if (result.AvgFramerate < 23)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.FramerateTooLow);
                }

                if (result.AvgFramerate > 61)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.FramerateTooHigh);
                }

                // // todo doesn't really work
                // // webm returns 0
                // Console.WriteLine(new { mediaInfo.Format.BitRate });
                // if (mediaInfo.Format.BitRate / 1000 < 500 && format != "webm")
                // {
                //     result.Warnings.Add(MediaAnalyserWarningKind.FakeVideo);
                // }
            }

            // Console.WriteLine(new { mediaInfo.PrimaryAudioStream!.BitRate });
            // webm returns 0
            if (format != "webm")
            {
                long kbps = mediaInfo.PrimaryAudioStream!.BitRate / 1000;
                result.AudioBitrateKbps = kbps;
                if (kbps < 89)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooLow);
                }

                if (kbps > 321)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooHigh);
                }
            }

            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-i \"{filePath}\" -map a:0 -af volumedetect -f null -",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                process.Start();
                string err = await process.StandardError.ReadToEndAsync();
                if (err.Any())
                {
                    string[] lines = err.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                    string[] volumedetectLines = lines.SkipWhile(x => !x.Contains("volumedetect")).ToArray();

                    string[] final = new string[volumedetectLines.Length];
                    for (int index = 0; index < volumedetectLines.Length; index++)
                    {
                        string volumedetectLine = volumedetectLines[index];
                        final[index] = new string(volumedetectLine.SkipWhile(c => c != ']').ToArray()[1..]);
                    }

                    result.VolumeDetect = final;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (!result.Warnings.Any())
            {
                result.IsValid = true;
            }

            result.Warnings = result.Warnings.OrderBy(x => x).ToList();
            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            result.Warnings.Add(MediaAnalyserWarningKind.UnknownError);
            return result;
        }
        finally
        {
            Console.WriteLine(JsonSerializer.Serialize(result, Utils.Jso));
        }
    }

    public static async Task<string> TranscodeInto192KMp3(string filePath)
    {
        Console.WriteLine("transcoding into .mp3");
        string outputFinal = $"{Path.GetTempFileName()}.mp3";
        const string audioEncoderName = "libmp3lame";

        var result = await Analyse(filePath, false, false);
        float volumeAdjust = GetVolumeAdjust(result);
        (string ss, string to) = await GetSsAndTo(filePath, new CancellationTokenSource().Token);

        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-i \"{filePath}\" " +
                    $"-ss {ss} " +
                    (to.Any() ? $"-to {to} " : "") +
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

        string err = await process.StandardError.ReadToEndAsync();
        if (err.Any())
        {
            Console.WriteLine(err.Last());
        }

        return outputFinal;
    }

    public static float GetVolumeAdjust(MediaAnalyserResult result)
    {
        float targetVolumeMean = -15.6f;
        float targetVolumeMax = -0.6f;

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
            throw new Exception("meanVolume is 0; this usually means the input file was inaccessible");
        }

        float volumeAdjust = 0;
        if (meanVolume > targetVolumeMean)
        {
            volumeAdjust = targetVolumeMean - meanVolume;
        }

        if (maxVolume > targetVolumeMax && volumeAdjust > targetVolumeMax)
        {
            volumeAdjust += targetVolumeMax - maxVolume;
        }

        Console.WriteLine($"volumeAdjust: {volumeAdjust}");
        return volumeAdjust;
    }

    public static async Task<(string ss, string to)> GetSsAndTo(string filePath, CancellationToken cancellationToken)
    {
        float silenceLeewaySeconds = 0.2f;

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
            string err = await process.StandardError.ReadToEndAsync(cancellationToken);
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
                                if (silenceStart > 0.4f)
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

        return (ss, to);
    }
}
