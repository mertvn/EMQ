using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum MCOptionKind
{
    Random = 0,
    Lists = 1,

    [Description("Other songs in the quiz")]
    SelectedSongs = 2,

    [Description("Same artist")]
    Artist = 3,

    [Description("Same artist pair")]
    ArtistPair = 4,

    [Description("Same developer")]
    Developer = 5,

    [Description("Common answers")]
    Qsh = 6,
}
