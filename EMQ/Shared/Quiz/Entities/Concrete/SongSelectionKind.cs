using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SongSelectionKind
{
    Random,
    Looting,

    [Description("Spaced repetition")]
    SpacedRepetition,
    LocalMusicLibrary = 777,
}
