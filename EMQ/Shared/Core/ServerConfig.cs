namespace EMQ.Shared.Core;

public class ServerConfig
{
    public bool AllowRegistration { get; set; } = false;

    public bool AllowGuests { get; set; } = true;

    public bool RememberGuestsBetweenServerRestarts { get; set; }

    public bool IsServerReadOnly { get; set; } // todo check this in more places

    public bool IsSubmissionDisabled { get; set; }

    public bool IsChristmasMode { get; set; }
}
