using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Title
{
    public string LatinTitle { get; set; } = "";

    public string? NonLatinTitle { get; set; }

    public string Language { get; set; } = "";

    public bool IsMainTitle { get; set; }

    /// added for EGS C/A/L matching purposes, don't rely on it being filled elsewhere
    public int ArtistAliasId { get; set; }

    public override string ToString()
    {
        return $"{LatinTitle}" +
               (!string.IsNullOrWhiteSpace(NonLatinTitle) && !string.Equals(NonLatinTitle, LatinTitle,
                   StringComparison.InvariantCultureIgnoreCase)
                   ? $" ({NonLatinTitle})"
                   : "");
    }
}
