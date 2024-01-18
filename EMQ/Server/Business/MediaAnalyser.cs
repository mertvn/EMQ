using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FFMpegCore;

namespace EMQ.Server.Business;

public static class MediaAnalyser
{
// todo detect bad transcodes
    public static async Task<MediaAnalyserResult> Analyse(string filePath, bool returnEarlyIfInvalidFormat = false)
    {
        string[] validAudioFormats = { "ogg", "mp3" };
        string[] validVideoFormats = { "mp4", "webm" };

        var result = new MediaAnalyserResult { IsValid = false, Warnings = new List<MediaAnalyserWarningKind>(), };

        try
        {
            Console.WriteLine("Analysing " + filePath);
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
            bool isVideo = true; // todo?
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
        var file = Path.GetFileNameWithoutExtension(filePath);
        string outputFinal = $"{Path.GetTempFileName()}.mp3";
        string audioEncoderName = "libmp3lame";

        // todo volumedetect
        // todo silencedetect
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-i \"{filePath}\" " +
                    $"-c:a {audioEncoderName} -b:a 192k -ac 2 " +
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
            Console.WriteLine(err);
        }

        return outputFinal;
    }
}
