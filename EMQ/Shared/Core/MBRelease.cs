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
