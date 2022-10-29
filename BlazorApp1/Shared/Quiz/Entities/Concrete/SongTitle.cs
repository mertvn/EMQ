using System;
using System.Collections.Generic;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class SongTitle
{
    public string LatinTitle { get; set; } = "";

    public string NonLatinTitle { get; set; } = "";

    public int Language { get; set; }
}
