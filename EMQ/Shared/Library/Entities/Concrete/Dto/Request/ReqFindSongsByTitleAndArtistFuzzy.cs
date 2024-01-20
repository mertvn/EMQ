using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByTitleAndArtistFuzzy
{
    public ReqFindSongsByTitleAndArtistFuzzy(List<string> titles, List<string> artists,
        SongSourceSongTypeMode songSourceSongTypeMode)
    {
        Titles = titles;
        Artists = artists;
        SongSourceSongTypeMode = songSourceSongTypeMode;
    }

    public List<string> Titles { get; }

    public List<string> Artists { get; }

    public SongSourceSongTypeMode SongSourceSongTypeMode { get; }
}
