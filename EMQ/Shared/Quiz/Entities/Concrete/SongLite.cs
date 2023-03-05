using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongLite
{
    public SongLite(List<Title> titles, List<SongLink> links, List<string> sourceVndbIds, List<string> artistVndbIds)
    {
        Titles = titles;
        Links = links;
        SourceVndbIds = sourceVndbIds;
        ArtistVndbIds = artistVndbIds;
    }

    public List<Title> Titles { get; set; }

    public List<SongLink> Links { get; set; }

    public List<string> SourceVndbIds { get; set; }

    public List<string> ArtistVndbIds { get; set; }
}
