﻿namespace EMQ.Server;

// attributes don't like Enum.ToString()...
public static class RateLimitKind
{
    public const string Login = "ratelimit-Login";

    public const string Register = "ratelimit-Register";

    public const string ForgottenPassword = "ratelimit-ForgottenPassword";

    public const string ValidateSession = "ratelimit-ValidateSession";

    public const string UploadFile = "ratelimit-UploadFile";

    public const string OnceEvery5Seconds = "ratelimit-OnceEvery5Seconds";
}
