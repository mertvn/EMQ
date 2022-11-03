using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db;

public class SongLite
{
    public List<Title> Titles { get; set; }

    public List<SongLink> Links { get; set; }

    public List<string> SourceVndbIds { get; set; }

    public List<string> ArtistVndbIds { get; set; }
}
