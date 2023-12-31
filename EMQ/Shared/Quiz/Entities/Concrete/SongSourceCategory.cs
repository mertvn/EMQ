using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class SongSourceCategory
{
    [ProtoMember(1)]
    public int Id { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("n")]
    public string Name { get; set; } = "";

    [ProtoMember(3)]
    [JsonPropertyName("v")]
    public string? VndbId { get; set; }

    [ProtoMember(4)]
    [JsonPropertyName("t")]
    public SongSourceCategoryType Type { get; set; } = SongSourceCategoryType.Unknown;

    [ProtoMember(5)]
    [Range(0, 3)]
    [JsonPropertyName("r")]
    public float? Rating { get; set; }

    [ProtoMember(6)]
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
