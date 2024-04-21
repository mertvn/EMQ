using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Library.Entities.Concrete;

public enum SongReportKind
{
    [Display(Name = "Other")]
    Other,

    [Display(Name = "Dead link")]
    DeadLink,

    [Display(Name = "Video or audio doesn't play")]
    VideoOrAudioDoesntPlay,

    [Display(Name = "Wrong song")]
    WrongSong,

    [Display(Name = "Wrong untranslated song title")]
    WrongUntranslatedSongTitle,

    [Display(Name = "Bad audio quality")]
    BadAudioQuality,

    [Display(Name = "File has unrelated content at the start or end")]
    UnrelatedContent,

    [Display(Name = "Fake video")]
    FakeVideo,

    [Display(Name = "Vocals under BGM type")]
    VocalsUnderBgm,

    [Display(Name = "Unmarked spoilers")]
    UunmarkedSpoilers,
}
