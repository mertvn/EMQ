using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongHint
{
    public List<Title> Titles { get; set; } = new();

    public List<SongArtist> Artists { get; set; } = new();

    public List<SongSource> Sources { get; set; } = new();
}
