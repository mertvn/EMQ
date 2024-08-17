using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongArtistLink
{
    public string Url { get; set; } = "";

    public SongArtistLinkType Type { get; set; } = SongArtistLinkType.Unknown;

    public string Name { get; set; } = "";
}

// todo? MAL
public enum SongArtistLinkType
{
    Unknown,

    [Description("VNDB staff")]
    VNDBStaff,

    [Description("MusicBrainz artist")]
    MusicBrainzArtist,

    [Description("VGMdb artist")]
    VGMdbArtist,

    [Description("ErogameScape creater")]
    ErogameScapeCreater,

    [Description("Anison.info person")]
    AnisonInfoPerson,

    [Description("Wikidata item")]
    WikidataItem,

    [Description("AniDB creator")]
    AniDBCreator,
}
