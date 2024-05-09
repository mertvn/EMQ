using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum GuessKind
{
    [Description("Source Title")]
    Mst,

    [Description("Artist Name")]
    A,

    [Description("Song Title")]
    Mt,

    [Description("Rigger Name")]
    Rigger
}
