using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum ScreenshotKind
{
    None,
    VN,

    [Description("VN cover")]
    VNCover,
    Character,
}
