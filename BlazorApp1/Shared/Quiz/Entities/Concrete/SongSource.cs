using System;
using System.Collections.Generic;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class SongSource
{
    public int Id { get; set; }

    public List<string> Aliases { get; set; } = new();

    public List<SongSourceCategory> Categories { get; set; } = new();

    // public string Medium { get; set; } // todo
}
