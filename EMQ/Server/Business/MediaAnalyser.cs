using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using FFMpegCore;

namespace EMQ.Server.Business;

public static class MediaAnalyser
{
    public static async Task<MediaAnalyserResult> Analyse(string filePath)
    {
        string[] validAudioFormats = { "ogg", "mp3" };
        string[] validVideoFormats = { "mp4", "webm" };

        var result = new MediaAnalyserResult
        {
            MediaInfo = null, IsValid = false, Warnings = new List<MediaAnalyserWarningKind>(),
        };

        try
        {
            Console.WriteLine("Analysing " + filePath);
            IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(filePath);
            result.MediaInfo = mediaInfo;

            // Console.WriteLine(new { mediaInfo.Duration });
            result.Duration = mediaInfo.Duration;
            if (mediaInfo.Duration < TimeSpan.FromSeconds(25))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooShort);
            }

            if (mediaInfo.Duration > TimeSpan.FromSeconds(720))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooLong);
            }

            // Console.WriteLine(new { mediaInfo.Format.FormatName });
            result.FormatList = mediaInfo.Format.FormatName;
            bool isVideo;
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
                    return result;
                }
            }

            result.IsVideo = isVideo;

            // Console.WriteLine(new { format });
            result.FormatSingle = format;
            if ($".{format}" != Path.GetExtension(filePath))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.WrongExtension);
            }

            if (isVideo)
            {
                // Console.WriteLine(new { mediaInfo.PrimaryVideoStream!.AvgFrameRate });
                result.AvgFramerate = mediaInfo.PrimaryVideoStream!.AvgFrameRate;
                if (mediaInfo.PrimaryVideoStream!.AvgFrameRate < 23)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.FramerateTooLow);
                }

                if (mediaInfo.PrimaryVideoStream!.AvgFrameRate > 61)
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
                if (kbps < 90)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooLow);
                }

                if (kbps > 320)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooHigh);
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
}

public enum MediaAnalyserWarningKind
{
    UnknownError,
    InvalidFormat,
    TooShort,
    TooLong,
    AudioBitrateTooLow,
    AudioBitrateTooHigh,
    FramerateTooLow,
    FramerateTooHigh,
    FakeVideo,
    WrongExtension,
}

public class MediaAnalyserResult
{
    [JsonIgnore]
    public IMediaAnalysis? MediaInfo { get; set; }

    public bool IsValid { get; set; }

    public List<MediaAnalyserWarningKind> Warnings { get; set; } = new();

    public string? FormatList { get; set; }

    public string? FormatSingle { get; set; }

    public bool IsVideo { get; set; }

    public double? AvgFramerate { get; set; }

    public long? AudioBitrateKbps { get; set; }

    public TimeSpan? Duration { get; set; }
}
