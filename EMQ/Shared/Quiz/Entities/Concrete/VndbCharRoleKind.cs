using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum VndbCharRoleKind
{
    [Description("Protagonist")]
    Main = 0,

    [Description("Main")]
    Primary = 1,

    [Description("Side")]
    Side = 2,

    [Description("Appears")]
    Appears = 3,
}
