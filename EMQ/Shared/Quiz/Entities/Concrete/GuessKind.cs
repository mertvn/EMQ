using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum GuessKind
{
    [Description("Source Title")]
    Mst = 0,

    [Description("Artist Name")]
    A = 1,

    [Description("Song Title")]
    Mt = 2,

    [Description("Rigger Name")]
    Rigger = 3,

    [Description("Developer Name")]
    Developer = 4,

    [Description("Composer Name")]
    Composer = 5,

    [Description("Arranger Name")]
    Arranger = 6,

    [Description("Lyricist Name")]
    Lyricist = 7,

    [Description("Character Name")]
    Character = 8,

    [Description("Illustrator Name")]
    Illustrator = 9,
}
