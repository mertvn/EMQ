using System;

namespace EMQ.Shared.Core;

public static class UploadConstants
{
    public const int MaxFilesizeBytes = 100 * 1024 * 1024; // 100 MB

    public const int MaxFilesPerRequest = 1;

    public const int MaxFilesSpecificSongUpload = 2;

    public const int MaxFilesBatchUpload = 50; // todo

    // todo? might need to apply this on the server as well
    public const int TimeoutSeconds = 30 * 60; // 30 minutes

    public static string SftpHost { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_HOST")!;

    public static string SftpUsername { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_USERNAME")!;

    public static string SftpPassword { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_PASSWORD")!;

    public static string SftpUserUploadDir { get; } = Environment.GetEnvironmentVariable("EMQ_SFTP_USER_UPLOAD_DIR")!;
}
