using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[Flags]
public enum SongAttributes
{
    None = 0,

    [Display(Name = "Self-explanatory.")]
    [Description("Video contains spoilers")]
    Spoilers = 1,

    [Description("Non-canon")]
    [Display(Name =
        "This song does not play in any VNs, but is related to at least one VN in some significant way. E.g.: character song albums, covers, arrangements etc.")]
    NonCanon = 2,

    [Description("Unofficial")]
    [Display(Name =
        "This song was published by a party other than the original developers/publishers of the VN. It also implies that the song is Non-canon.")]
    Unofficial = 4,

    [Display(Name = "Self-explanatory.")]
    [Description("Video contains flashing lights")]
    FlashingLights = 8,

    [Display(Name = "Edits and uploads are not allowed.")]
    Locked = 16,
}
