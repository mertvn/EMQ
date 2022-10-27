using System;
using System.Collections.Generic;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class SongLink
{
    public string Url { get; set; } = "";

    public bool IsVideo { get; set; }
}
