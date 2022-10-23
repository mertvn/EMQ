using System;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class SongSource
{
    public SongSource(string[] aliases
        // , string medium = ""
    )
    {
        Aliases = aliases;
        // Medium = medium;
    }

    public string[] Aliases { get; set; }

    // public string Medium { get; set; }
}
