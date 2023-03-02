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
            IsValid = false, Warnings = new List<MediaAnalyserWarningKind>(), MediaInfo = null
        };

        try
        {
            Console.WriteLine("Analysing " + filePath);
            IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(filePath);
            result.MediaInfo = mediaInfo;

            Console.WriteLine(new { mediaInfo.Format.FormatName });
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

            Console.WriteLine(new { format });
            if ($".{format}" != Path.GetExtension(filePath))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.WrongExtension);
            }

            if (isVideo)
            {
                Console.WriteLine(new { mediaInfo.PrimaryVideoStream!.AvgFrameRate });
                if (mediaInfo.PrimaryVideoStream!.AvgFrameRate < 24)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.FramerateTooLow);
                }

                if (mediaInfo.PrimaryVideoStream!.AvgFrameRate > 60)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.FramerateTooHigh);
                }

                // todo doesn't really work
                // // webm returns 0
                // Console.WriteLine(new { mediaInfo.Format.BitRate });
                // if (mediaInfo.Format.BitRate / 1000 < 500 && !mediaInfo.Format.FormatName.Contains("webm"))
                // {
                //     result = MediaAnalyserResult.FakeVideo;
                //     return result;
                // }
            }

            Console.WriteLine(new { mediaInfo.PrimaryAudioStream!.BitRate });
            // webm returns 0
            if (format != "webm")
            {
                long kbps = mediaInfo.PrimaryAudioStream!.BitRate / 1000;
                if (kbps < 90)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooLow);
                }

                if (kbps > 320)
                {
                    result.Warnings.Add(MediaAnalyserWarningKind.AudioBitrateTooHigh);
                }
            }

            Console.WriteLine(new { mediaInfo.Duration });
            if (mediaInfo.Duration < TimeSpan.FromSeconds(25))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooShort);
            }

            if (mediaInfo.Duration > TimeSpan.FromSeconds(720))
            {
                result.Warnings.Add(MediaAnalyserWarningKind.TooLong);
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
    public bool IsValid { get; set; }

    public List<MediaAnalyserWarningKind> Warnings { get; set; } = new();

    [JsonIgnore]
    public IMediaAnalysis? MediaInfo { get; set; }
}
