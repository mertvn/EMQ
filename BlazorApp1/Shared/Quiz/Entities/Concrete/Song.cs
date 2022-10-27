using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class Song
{
    public int Id { get; set; }

    public string LatinTitle { get; set; } = "";

    public List<SongArtist> Artists { get; set; } = new();

    public List<SongLink> Links { get; set; } = new();

    [JsonIgnore]
    public string Data { get; set; } = "";

    // todo type

    public List<SongSource> Sources { get; set; } = new();
}
