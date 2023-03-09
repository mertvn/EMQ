using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSourceCategory
{
    public int Id { get; set; }

    [JsonPropertyName("n")]
    public string Name { get; set; } = "";

    [JsonPropertyName("v")]
    public string? VndbId { get; set; }

    [JsonPropertyName("t")]
    public SongSourceCategoryType Type { get; set; } = SongSourceCategoryType.Unknown;

    [Range(0, 3)]
    [JsonPropertyName("r")]
    public float? Rating { get; set; }

    [JsonPropertyName("s")]
    public SpoilerLevel? SpoilerLevel { get; set; }
}

public enum SpoilerLevel
{
    None = 0,
    Minor = 1,
    Major = 2,
}

public enum SongSourceCategoryType
{
    Unknown,
    Tag,
    Genre
}
