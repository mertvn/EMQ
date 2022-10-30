using System;
using System.Collections.Generic;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class SongArtist
{
    public int Id { get; set; }

    public List<string> Aliases { get; set; } = new();
}
