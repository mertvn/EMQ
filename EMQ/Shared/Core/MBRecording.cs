using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace EMQ.Shared.Core;

public class MbArtist
{
    [JsonPropertyName("sort-name")]
    public string sortname { get; set; } = "";

    [JsonPropertyName("id")]
    public string id { get; set; } = "";

    [JsonPropertyName("type-id")]
    public string typeid { get; set; } = "";

    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";

    [JsonPropertyName("type")]
    public string type { get; set; } = "";
}

public class MbArtistCredit
{
    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    [JsonPropertyName("artist")]
    public MbArtist MbArtist { get; set; } = new();

    [JsonPropertyName("joinphrase")]
    public string joinphrase { get; set; } = "";
}

public class MBRecording
{
    [JsonPropertyName("title")]
    public string title { get; set; } = "";

    [JsonPropertyName("artist-credit")]
    public List<MbArtistCredit> artistcredit { get; set; } = new();

    [JsonPropertyName("id")]
    public string id { get; set; } = "";
}
