using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongLite
{
    public List<Title> Titles { get; set; }

    public List<SongLink> Links { get; set; }

    public List<string> SourceVndbIds { get; set; }

    public List<string> ArtistVndbIds { get; set; }

    public string Md5Hash { get; set; }
}
