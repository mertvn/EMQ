using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace EMQ.Shared.Core;

public class MBMedium
{
    [JsonPropertyName("position")]
    public int position { get; set; }

    [JsonPropertyName("tracks")]
    public List<MBTrack> tracks { get; set; } = new();
}

public class MBRelease
{
    // public string date { get; set; } = "";

    public string country { get; set; } = "";

    // public string title { get; set; } = "";

    [JsonPropertyName("media")]
    public List<MBMedium> media { get; set; } = new();
}

public class MBTrack
{
    [JsonPropertyName("title")]
    public string title { get; set; } = "";

    [JsonPropertyName("recording")]
    public MBRecording? recording { get; set; }

    [JsonPropertyName("position")]
    public int position { get; set; }
}

public class MBReleaseGroup
{
    [JsonPropertyName("first-release-date")]
    public string firstreleasedate { get; set; } = "";

    public string title { get; set; } = "";

    public List<MBRelease> releases { get; set; } = new();

    [JsonPropertyName("artist-credit")]
    public List<MbArtistCredit> artistcredit { get; set; } = new();
}
