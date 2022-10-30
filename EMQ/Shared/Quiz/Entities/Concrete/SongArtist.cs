using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongArtist
{
    public int Id { get; set; }

    public List<string> Aliases { get; set; } = new();
}
