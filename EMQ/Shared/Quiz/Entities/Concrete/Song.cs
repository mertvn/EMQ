using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[Cloneable.Cloneable]
public partial class Song
{
    public int Id { get; set; }

    [JsonIgnore]
    public bool DoneBuffering { get; set; }

    public int StartTime { get; set; } // todo move out of this class

    public string ScreenshotUrl { get; set; } = ""; // todo move out of this class

    public string CoverUrl { get; set; } = ""; // todo move out of this class

    public DateTime PlayedAt { get; set; } // todo move out of this class

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

    public List<Guid> MusicBrainzTracks { get; set; } = new();

    public SongAttributes Attributes { get; set; } = SongAttributes.None;

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

    public string ToStringLatin()
    {
        var first = Titles.FirstOrDefault(x => x.Language == "ja" && x.IsMainTitle) ?? Titles.FirstOrDefault();
        var firstSource = Sources.FirstOrDefault(x => x.Titles.Any(y => y.Language == "ja" && y.IsMainTitle)) ??
                          Sources.FirstOrDefault();

        if (first == null || firstSource is not { Id: > 0 })
        {
            return "";
        }

        return
            $"{(firstSource.Titles.FirstOrDefault(x => x.Language == "ja" && x.IsMainTitle) ?? firstSource.Titles.First()).LatinTitle} {firstSource.SongTypes.First().ToString()} {first.LatinTitle}";
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
