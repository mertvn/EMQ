using System.Collections.Generic;

namespace EMQ.Shared.Library.Entities.Concrete;

public struct LibraryStats
{
    public int TotalMusicCount { get; set; }

    public int TotalMusicSourceCount { get; set; }

    public int TotalArtistCount { get; set; }

    public int AvailableMusicCount { get; set; }

    public int AvailableMusicSourceCount { get; set; }

    public int AvailableArtistCount { get; set; }

    public int VideoLinkCount { get; set; }

    public int SoundLinkCount { get; set; }

    public int BothLinkCount { get; set; }

    public List<LibraryStatsMsm> msm { get; set; }

    public List<LibraryStatsMsm> msmAvailable { get; set; }

    public List<LibraryStatsAm> am { get; set; }

    public List<LibraryStatsAm> amAvailable { get; set; }
}

public struct LibraryStatsMsm
{
    public int MId { get; set; }

    public string MstLatinTitle { get; set; }

    public string MselUrl { get; set; }

    public int MusicCount { get; set; }
}

public struct LibraryStatsAm
{
    public int AId { get; set; }

    public string AALatinAlias { get; set; }

    public string VndbId { get; set; }

    public int MusicCount { get; set; }
}
