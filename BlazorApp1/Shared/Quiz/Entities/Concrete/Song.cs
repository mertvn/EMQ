using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class Song
{
    public int Id { get; set; }

    [JsonIgnore]
    public string Data { get; set; } = "";

    public List<SongTitle> Titles { get; set; } = new();

    public List<SongArtist> Artists { get; set; } = new();

    public List<SongLink> Links { get; set; } = new();

    public int Type { get; set; } // todo

    public List<SongSource> Sources { get; set; } = new();
}
