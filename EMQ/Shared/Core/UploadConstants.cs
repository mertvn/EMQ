using System;
using System.Linq;

namespace EMQ.Shared.Core;

public static class UploadConstants
{
    public const int MaxFilesizeBytes = 500 * 1024 * 1024; // 500 MB

    public const int MaxFilesPerRequest = 1;

    public const int MaxFilesSpecificSongUpload = 20;

    public const int MaxFilesBatchUpload = 200;

    // todo? might need to apply this on the server as well
    public const int TimeoutSeconds = 30 * 60; // 30 minutes

    public const int MaxConcurrentTranscodes = 2;

    public const int MaxConcurrentEncodes = 1;

    public static string SftpHost { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_HOST")!;

    public static string SftpUsername { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_USERNAME")!;

    public static string SftpPassword { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_PASSWORD")!;

    public static string SftpUserUploadDir { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_USER_UPLOAD_DIR")!;

    public static string[] PossibleMetadataRecordingIdNames { get; set; } =
    {
        "MUSICBRAINZ_TRACKID", "MusicBrainz Recording Id", "MUSICBRAINZ_RELEASETRACKID",
        "MusicBrainz Release Track Id",
    };

    public static MediaTypeInfo[] ValidMediaTypes { get; set; } =
    {
        // todo signatures
        // video formats
        new MediaTypeInfo
        {
            IsVideoFormat = true,
            Extension = "webm",
            MimeType = "video/webm",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = false,
        },

        // audio formats
        new MediaTypeInfo
        {
            IsVideoFormat = false,
            Extension = "mp3",
            MimeType = "audio/mpeg",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = false,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = false,
            Extension = "ogg",
            MimeType = "video/ogg",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = false,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = false,
            Extension = "ogg",
            MimeType = "application/ogg",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = false,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = false,
            Extension = "ogg",
            MimeType = "audio/ogg",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = false,
        },

        // video formats that require encoding
        new MediaTypeInfo
        {
            IsVideoFormat = true,
            Extension = "mpg",
            MimeType = "video/mpeg",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = true,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = true,
            Extension = "mp4",
            MimeType = "video/mp4",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = true,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = true,
            Extension = "wmv",
            MimeType = "video/x-ms-wmv",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = true,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = true,
            Extension = "avi",
            MimeType = "video/avi",
            Signature = "",
            RequiresTranscode = false,
            RequiresEncode = true,
        },
        // https://bugzilla.mozilla.org/show_bug.cgi?id=1451786
        // new MediaTypeInfo
        // {
        //     Extension = "mkv",
        //     MimeType = "video/x-matroska",
        //     Signature = "",
        //     RequiresTranscode = false,
        //     RequiresEncode = true,
        // },
        // problematic because we can't differentiate it from audio/ogg right now
        // new MediaTypeInfo
        // {
        //     Extension = "ogv",
        //     MimeType = "video/ogg",
        //     Signature = "",
        //     RequiresTranscode = false,
        //     RequiresEncode = true,
        // },

        // audio formats that require transcoding
        new MediaTypeInfo
        {
            IsVideoFormat = false,
            Extension = "flac",
            MimeType = "audio/flac",
            Signature = "",
            RequiresTranscode = true,
            RequiresEncode = false,
        },
        new MediaTypeInfo
        {
            IsVideoFormat = false,
            Extension = "wav",
            MimeType = "audio/wav",
            Signature = "",
            RequiresTranscode = true,
            RequiresEncode = false,
        },
    };

    public static string ValidMediaTypesStr { get; set; } =
        string.Join(", ", ValidMediaTypes.DistinctBy(x => x.Extension).Select(x => x.Extension));

    public static MediaTypeInfo[] ValidMediaTypesBgm { get; set; } =
        ValidMediaTypes.Where(x => !x.IsVideoFormat).ToArray();

    public static string ValidMediaTypesBgmStr { get; set; } =
        string.Join(", ", ValidMediaTypesBgm.DistinctBy(x => x.Extension).Select(x => x.Extension));
}

public record MediaTypeInfo
{
    public string Extension = "";

    public string MimeType = "";

    public string Signature = "";

    public bool RequiresTranscode = false;

    public bool RequiresEncode = false;

    public bool IsVideoFormat = false;
}
