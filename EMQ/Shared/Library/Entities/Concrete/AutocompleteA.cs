using System.Text.Json.Serialization;
using ProtoBuf;

namespace EMQ.Shared.Library.Entities.Concrete;

[ProtoContract]
public class AutocompleteA
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public AutocompleteA()
    {
    }

    [JsonConstructor]
    public AutocompleteA(int aId,
        // string vndbId,
        string aaLatinAlias,
        // int aaId,
        string aaNonLatinAlias = "")
    {
        AId = aId;
        // VndbId = vndbId;
        AALatinAlias = aaLatinAlias;
        AANonLatinAlias = aaNonLatinAlias;
        // AAId = aaId;
    }

    [ProtoMember(1)]
    [JsonPropertyName("aId")]
    public int AId { get; set; }

    // [ProtoMember(2)]
    // [JsonPropertyName("vndbId")]
    // public string VndbId { get; set; } = null!;

    [ProtoMember(3)]
    [JsonPropertyName("aaLA")]
    public string AALatinAlias { get; set; } = null!;

    [ProtoMember(4)]
    [JsonPropertyName("aaNLA")]
    public string AANonLatinAlias { get; set; } = null!;

    // [ProtoMember(5)]
    // [JsonPropertyName("aaId")]
    // public int AAId { get; set; }
}
