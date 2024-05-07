using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SongHintKind
{
    [Description("Source Song Type")]
    Msst,

    [Description("Artist Name")]
    A,

    [Description("Song Title")]
    Mt
}
