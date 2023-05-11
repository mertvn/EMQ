using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Song
{
    public int Id { get; set; }

    [JsonIgnore]
    public string Data { get; set; } = "";

    public int StartTime { get; set; }

    public List<Title> Titles { get; set; } = new();

    public List<SongArtist> Artists { get; set; } = new();

    public List<SongLink> Links { get; set; } = new();

    public SongType Type { get; set; } = SongType.Unknown;

    public List<SongSource> Sources { get; set; } = new();

    [JsonIgnore]
    public List<string> ProducerIds { get; set; } = new();

    [JsonIgnore]
    public Dictionary<int, List<Label>> PlayerLabels { get; set; } = new();

    public SongStats Stats { get; set; } = new();
}

public enum SongType
{
    Unknown,
    Standard,
    Instrumental,
    Chanting,
    Character
}
