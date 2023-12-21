using System;
using System.Text.RegularExpressions;

namespace EMQ.Shared.Core;

public static class RegexPatterns
{
    public const string SongLinkUrlRegex = @"^https:\/\/.+\..+\/.+\.(ogg|mp3|mp4|webm)$";

    public const string VndbIdRegex = @"^u[0-9]{1,99}$";

    // do not start allowing @ in usernames without modifying usernameOrEmail code
    public const string UsernameRegex = @"^[a-zA-Z0-9_-]{2,16}$";

    public static readonly Regex UsernameRegexCompiled =
        new(UsernameRegex, RegexOptions.Compiled, TimeSpan.FromMilliseconds(420));

    public const string EmailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    public static readonly Regex EmailRegexCompiled =
        new(EmailRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(690));
}
