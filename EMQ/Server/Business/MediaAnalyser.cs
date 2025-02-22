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
using EMQ.Client;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;

namespace EMQ.Server.Business;

public static class MediaAnalyser
{
    public static readonly SemaphoreSlim SemaphoreEncode = new(UploadConstants.MaxConcurrentEncodes);

    public static readonly SemaphoreSlim SemaphoreTranscode = new(UploadConstants.MaxConcurrentTranscodes);

    // todo detect bad transcodes
    public static async Task<MediaAnalyserResult> Analyse(string filePath, bool returnEarlyIfInvalidFormat = false,
        bool? isVideoOverride = null, int rqId = 0)
    {
        string[] validAudioFormats = { "ogg", "mp3" };
        string[] validVideoFormats = { "mp4", "webm" };

        var result = new MediaAnalyserResult
        {
            IsValid = false, Warnings = new List<MediaAnalyserWarningKind>(), Timestamp = DateTime.UtcNow
        };

        try
        {
            Console.WriteLine("Analysing " + filePath);

            await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            result.Sha256 = CryptoUtils.Sha256Hash(fs);
            await fs.DisposeAsync();

            IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(filePath);

            // Console.WriteLine(new { mediaInfo.Duration });
            result.Duration = mediaInfo.Duration;
            if (mediaInfo.Duration < TimeSpan.FromSeconds(2))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooShort);
            }

            if (mediaInfo.Duration > TimeSpan.FromSeconds(900))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooLong);
            }

            result.PrimaryAudioStreamCodecName =
                mediaInfo.PrimaryAudioStream?.CodecName ?? mediaInfo.AudioStreams.FirstOrDefault()?.CodecName;
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

            long filesizeBytes = new FileInfo(filePath).Length;
            result.FilesizeMb = (float)filesizeBytes / 1024 / 1024;
            result.OverallBitrateKbps = ((filesizeBytes * 8) / result.Duration!.Value.TotalSeconds) / 1000;
            if (result.OverallBitrateKbps != 0 && result.OverallBitrateKbps > 3400)
            {
                result.Warnings.Add(MediaAnalyserWarningKind.OverallBitrateTooHigh);
            }

            if (isVideoOverride != null)
            {
                isVideo = isVideoOverride.Value;
                if (string.Equals($".weba", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
                {
                    format = "weba";
                }
            }

            result.IsVideo = isVideo;

            // Console.WriteLine(new { format });
            result.FormatSingle = format;
            if (!string.Equals($".{format}", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.WrongExtension);
            }

            if (isVideo)
            {
                // Console.WriteLine(new { mediaInfo.PrimaryVideoStream!.AvgFrameRate });
                result.AvgFramerate = mediaInfo.PrimaryVideoStream!.AvgFrameRate;
                result.Width = mediaInfo.PrimaryVideoStream.Width;
                result.Height = mediaInfo.PrimaryVideoStream.Height;
                result.VideoBitrateKbps = mediaInfo.PrimaryVideoStream.BitRate / 1000;

                if (result.AvgFramerate is 1000 or double.NaN)
                {
                    result.AvgFramerate = mediaInfo.PrimaryVideoStream!.FrameRate;
                }

                if (result.AvgFramerate < 7)
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
                long kbps;
                if (format == "weba")
                {
                    kbps = (long)result.OverallBitrateKbps!.Value;
                }
                else
                {
                    kbps = mediaInfo.PrimaryAudioStream?.BitRate / 1000 ?? (long)result.OverallBitrateKbps!.Value;
                }

                result.AudioBitrateKbps = kbps;
                if (kbps < 120)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooLow);
                }

                if (kbps > 500)
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
                    // Console.WriteLine(err);
                    string[] lines = err.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                    string[] volumedetectLines =
                        lines.Where(x =>
                                x.Contains("Parsed_volumedetect", StringComparison.OrdinalIgnoreCase) &&
                                !x.Contains("n_samples: 0", StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                    string[] final = new string[volumedetectLines.Length];
                    for (int index = 0; index < volumedetectLines.Length; index++)
                    {
                        string volumedetectLine = volumedetectLines[index];
                        final[index] = new string(volumedetectLine.SkipWhile(c => c != ']').ToArray()[1..]);
                    }

                    result.VolumeDetect = final;
                }
                else
                {
                    Console.WriteLine(await process.StandardOutput.ReadToEndAsync());
                    throw new Exception("failed to volumedetect");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (rqId > 0)
            {
                try
                {
                    string guid = filePath.LastSegment();
                    string soxFilename = $"{guid}.png";
                    string soxOut = $"{Path.GetTempPath()}{soxFilename}";
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "sox",
                            Arguments =
                                $"\"{filePath}\" -n remix 1,2 spectrogram -x 500 -y 250 -t \"RQ{rqId} {guid}\" -o {soxOut}",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false,
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                    if (File.Exists(soxOut))
                    {
                        // yeah idk about doing this here
                        await using FileStream fsSox = new(soxOut, FileMode.Open, FileAccess.Read);
                        ServerUtils.SftpFileUpload(
                            UploadConstants.SftpHost, UploadConstants.SftpUsername,
                            UploadConstants.SftpPassword,
                            fsSox,
                            Path.Combine(UploadConstants.SftpUserUploadDir, "sox", soxFilename)
                                .Replace("\\", "/")); // imagine having to do this in 2025
                        await fsSox.DisposeAsync();
                        File.Delete(soxOut);
                    }
                    else
                    {
                        throw new Exception("failed to sox");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
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

    public static async Task<string> EncodeIntoWebm(string filePath, int threads, UploadOptions uploadOptions,
        CancellationToken cancellationToken, string? outputFinal = null)
    {
        Console.WriteLine("encoding into .webm");
        outputFinal ??= $"{Path.GetTempFileName()}.webm";
        var result = await MediaAnalyser.Analyse(filePath);
        Console.WriteLine(JsonSerializer.Serialize(result, Utils.JsoIndented));

        // wmapro sources have clicks when converted to ogg or opus for some reason
        string[] blacklistedAudioFormats = { "wmapro" };
        string[] copiableAudioFormats = { "vorbis", "opus" };

        const string audioEncoderName = "libopus";
        const int maxVideoBitrateKbps = 2500;

        if (blacklistedAudioFormats.Contains(result.PrimaryAudioStreamCodecName))
        {
            throw new Exception("Audio codec in the source video is unprocessable.");
        }

        bool requiresDownscale = result.Width * result.Height > 1280 * 768;
        bool canCopyAudio = copiableAudioFormats.Contains(result.PrimaryAudioStreamCodecName);
        bool encodeAudioSeparately = !canCopyAudio && false;
        bool cropSilence = uploadOptions.ShouldCropSilence;
        bool doTwoPass = uploadOptions.DoTwoPass;
        float volumeAdjust = uploadOptions.ShouldAdjustVolume ? MediaAnalyser.GetVolumeAdjust(result) : 0;
        // volumeAdjust = 13;

        string ss = TimeSpan.FromSeconds(0).ToString("c");
        string to = "";
        if (cropSilence)
        {
            (ss, to) = await MediaAnalyser.GetSsAndTo(filePath, cancellationToken);
        }

        if (encodeAudioSeparately)
        {
            // todo
            throw new NotImplementedException();
        }
        else
        {
            // todo copy audio
            string args = $"-i \"{filePath}\" " + $"-ss {ss} " + (to.Any() ? $"-to {to} " : "") +
                          $"-map 0:v " +
                          $"-map 0:a? " + $"-shortest " +
                          $"-c:v libvpx-vp9 -b:v {maxVideoBitrateKbps}k -crf 28 -pix_fmt yuv420p " +
                          $"-deadline good -cpu-used 3 -tile-columns 2 -threads {threads} -row-mt 1 " +
                          $"-g 100 " +
                          (requiresDownscale ? "-vf \"scale=-1:720,setsar=1\" " : "") +
                          $"-c:a {audioEncoderName} -b:a 320k -ac 2 -af \"volume={volumeAdjust.ToString(CultureInfo.InvariantCulture)}dB\" " +
                          $"-nostdin \"{outputFinal}\"";

            if (doTwoPass)
            {
                string argsPass1 = args.Replace($"-nostdin \"{outputFinal}\"", "-nostdin -pass 1 -an -f null -");
                {
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "ffmpeg",
                            Arguments = argsPass1,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();

                    string err = await process.StandardError.ReadToEndAsync(cancellationToken);
                    if (err.Any())
                    {
                        Console.WriteLine(err);
                    }
                }

                string argsPass2 = args.Replace($"-g 100 ", $"-g 100 -pass 2 ");
                {
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "ffmpeg",
                            Arguments = argsPass2,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();

                    string err = await process.StandardError.ReadToEndAsync(cancellationToken);
                    if (err.Any())
                    {
                        Console.WriteLine(err);
                    }
                }
            }
            else
            {
                {
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "ffmpeg",
                            Arguments = args,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();

                    string err = await process.StandardError.ReadToEndAsync(cancellationToken);
                    if (err.Any())
                    {
                        Console.WriteLine(err);
                    }
                }
            }
        }

        return outputFinal;
    }

    public static async Task<string> TranscodeInto192KMp3(string filePath, UploadOptions uploadOptions,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("transcoding into .mp3");
        string outputFinal = $"{Path.GetTempFileName()}.mp3";
        const string audioEncoderName = "libmp3lame";

        var result = await Analyse(filePath, false, false);
        float volumeAdjust = uploadOptions.ShouldAdjustVolume ? GetVolumeAdjust(result) : 0;

        string ss = TimeSpan.FromSeconds(0).ToString("c");
        string to = "";
        if (uploadOptions.ShouldCropSilence)
        {
            (ss, to) = await GetSsAndTo(filePath, cancellationToken);
        }

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

        string err = await process.StandardError.ReadToEndAsync(cancellationToken);
        if (err.Any())
        {
            Console.WriteLine(err.Last());
        }

        return outputFinal;
    }

    public static float GetVolumeAdjust_Inner(float meanVolume, float maxVolume)
    {
        const float targetVolumeMean = -15.0f;
        const float targetVolumeMax = -0.5f;

        float volumeAdjust = 0;
        if (meanVolume > targetVolumeMean)
        {
            volumeAdjust = targetVolumeMean - meanVolume;
        }
        else if (meanVolume < targetVolumeMean && maxVolume < targetVolumeMax)
        {
            float maxAdjustMax = Math.Abs(maxVolume - targetVolumeMax);
            float maxAdjustMean = Math.Abs(meanVolume - targetVolumeMean);
            float min = maxAdjustMax > maxAdjustMean ? maxAdjustMean : maxAdjustMax;
            float max = maxAdjustMax > maxAdjustMean ? maxAdjustMax : maxAdjustMean;
            volumeAdjust = Math.Clamp(volumeAdjust, min, max);
        }

        if (maxVolume > targetVolumeMax && volumeAdjust > targetVolumeMax)
        {
            volumeAdjust += targetVolumeMax - maxVolume;
        }

        Console.WriteLine($"volumeAdjust: {volumeAdjust}");
        return volumeAdjust;
    }

    public static float GetVolumeAdjust(MediaAnalyserResult result)
    {
        if (result.VolumeDetect == null || !result.VolumeDetect.Any())
        {
            return 0;
        }

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

        float volumeAdjust = GetVolumeAdjust_Inner(meanVolume, maxVolume);
        return volumeAdjust;
    }

    public static async Task<(string ss, string to)> GetSsAndTo(string filePath, CancellationToken cancellationToken)
    {
        const float silenceLeewaySeconds = 0.2f;
        const float silenceStartTolerance = 0.4f;

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

                            if (silenceStart <= silenceStartTolerance)
                            {
                                // start silence
                                ss = TimeSpan.FromSeconds(silenceEnd - silenceLeewaySeconds).ToString("c");
                            }
                            else // todo? this can produce 0s files sometimes
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

                                if (silenceStart > silenceStartTolerance)
                                {
                                    throw new Exception("case 4 silence_start not 0");
                                }

                                // silenceEnd -= silenceStart;
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
