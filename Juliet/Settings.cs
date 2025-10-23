using System.ComponentModel;

namespace Juliet;

public static class Settings
{
    [DefaultValue(true)]
    public static bool WaitWhenThrottled { get; set; } = true;

    [DefaultValue(2000)]
    public static int MaxResults { get; set; } = 2000;
}
