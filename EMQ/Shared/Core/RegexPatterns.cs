using System.Text.RegularExpressions;

namespace EMQ.Shared.Core;

public static class RegexPatterns
{
    public const string SongLinkUrlRegex = @"^.+\..+\/.+\.(ogg|mp3|mp4|webm)$";

    public const string VndbIdRegex = @"^u[0-9]{1,99}$";
}
