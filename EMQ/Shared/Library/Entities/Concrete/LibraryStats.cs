using System;
using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete;

public struct LibraryStats
{
    public int TotalMusicCount { get; set; }

    public int TotalMusicSourceCount { get; set; }

    public int TotalArtistCount { get; set; }

    public int AvailableMusicCount { get; set; }

    public int AvailableMusicSourceCount { get; set; }

    public int AvailableArtistCount { get; set; }

    public List<LibraryStatsMusicType> TotalLibraryStatsMusicType { get; set; }

    public List<LibraryStatsMusicType> AvailableLibraryStatsMusicType { get; set; }

    public int VideoLinkCount { get; set; }

    public int SoundLinkCount { get; set; }

    public int BothLinkCount { get; set; }

    public List<LibraryStatsMsm> msm { get; set; }

    public List<LibraryStatsMsm> msmAvailable { get; set; }

    public List<LibraryStatsMsm> msmNoBgm { get; set; }

    public List<LibraryStatsMsm> msmAvailableNoBgm { get; set; }

    public List<LibraryStatsAm> am { get; set; }

    public List<LibraryStatsAm> amAvailable { get; set; }

    public List<LibraryStatsAm> amNoBgm { get; set; }

    public List<LibraryStatsAm> amAvailableNoBgm { get; set; }

    public Dictionary<DateTime, int> msYear { get; set; }

    public Dictionary<DateTime, int> msYearAvailable { get; set; }

    public Dictionary<string, int> UploaderCounts { get; set; }

    public Dictionary<SongDifficultyLevel, int> SongDifficultyLevels { get; set; }
}

public class LibraryStatsMsm
{
    public int MSId { get; set; }

    public string MstLatinTitle { get; set; } = "";

    public string MselUrl { get; set; } = "";

    public int MusicCount { get; set; }

    public int AvailableMusicCount { get; set; }
}

public class LibraryStatsAm
{
    public int AId { get; set; }

    public string AALatinAlias { get; set; } = "";

    public string VndbId { get; set; } = "";

    public int MusicCount { get; set; }

    public int AvailableMusicCount { get; set; }
}

public class LibraryStatsMusicType
{
    public SongSourceSongType Type { get; set; }

    public int MusicCount { get; set; }
}
