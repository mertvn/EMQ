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
    public bool DoneBuffering { get; set; }

    public int StartTime { get; set; }

    public string ScreenshotUrl { get; set; } = "";

    public string CoverUrl { get; set; } = "";

    public DateTime PlayedAt { get; set; }

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

    public Guid? MusicBrainzRecordingGid { get; set; }

    public List<Guid> MusicBrainzReleases { get; set; } = new();

    public List<int> VgmdbAlbums { get; set; } = new();

    public override string ToString()
    {
        var first = Titles.FirstOrDefault(x => x.Language == "ja" && x.IsMainTitle) ?? Titles.First();
        var firstSource = Sources.FirstOrDefault(x => x.Titles.Any(y => y.Language == "ja" && y.IsMainTitle)) ??
                          Sources.First();
        return
            $"{(firstSource.Titles.FirstOrDefault(x => x.Language == "ja" && x.IsMainTitle) ?? firstSource.Titles.First()).LatinTitle} {firstSource.SongTypes.First().ToString()} {first.LatinTitle}" +
            (!string.IsNullOrWhiteSpace(first.NonLatinTitle) && !string.Equals(first.NonLatinTitle, first.LatinTitle,
                StringComparison.InvariantCultureIgnoreCase)
                ? $" ({first.NonLatinTitle})"
                : "");
    }
}

public enum SongType
{
    Unknown,
    Standard,
    Instrumental,
    Chanting,
    Character
}
