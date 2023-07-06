using System.ComponentModel;

namespace Juliet;

public static class Settings
{
    [DefaultValue(true)]
    public static bool WaitWhenThrottled { get; set; } = true;
}
