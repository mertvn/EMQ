using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class RQ
{
    public int id { get; set; }

    public int music_id { get; set; }

    public string url { get; set; } = "";

    public SongLinkType type { get; set; }

    public bool is_video { get; set; }

    public string submitted_by { get; set; } = "";

    public DateTime submitted_on { get; set; }

    public ReviewQueueStatus status { get; set; }

    public string? reason { get; set; }

    public string? analysis { get; set; }

    public Song Song { get; set; } = new();

    public TimeSpan? duration { get; set; }

    public MediaAnalyserResult? analysis_raw { get; set; }

    public string? sha256 { get; set; }
}

// todo move
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
    OverallBitrateTooHigh,
}

public class MediaAnalyserResult
{
    public bool IsValid { get; set; }

    public List<MediaAnalyserWarningKind> Warnings { get; set; } = new();

    public string? FormatList { get; set; }

    public string? FormatSingle { get; set; }

    public bool IsVideo { get; set; }

    public double? AvgFramerate { get; set; }

    public long? AudioBitrateKbps { get; set; }

    public TimeSpan? Duration { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? VideoBitrateKbps { get; set; }

    public double? OverallBitrateKbps { get; set; }

    public string[]? VolumeDetect { get; set; }

    public float? FilesizeMb { get; set; }

    public string? PrimaryAudioStreamCodecName { get; set; }

    public string? Sha256 { get; set; }

    public DateTime? Timestamp { get; set; }

    public bool? EncodedByEmq { get; set; }
}
