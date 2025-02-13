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
        string aaNonLatinAlias = "",
        bool isMain = true)
    {
        AId = aId;
        // VndbId = vndbId;
        AALatinAlias = aaLatinAlias;
        AANonLatinAlias = aaNonLatinAlias;
        IsMain = isMain;
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

    [ProtoMember(6)]
    [JsonPropertyName("aaLANorm")]
    public string AALatinAliasNormalized { get; set; } = null!;

    [ProtoMember(7)]
    [JsonPropertyName("aaNLANorm")]
    public string AANonLatinAliasNormalized { get; set; } = null!;

    [ProtoMember(8)]
    [JsonPropertyName("aaLANormR")]
    public string AALatinAliasNormalizedReversed { get; set; } = null!;

    [ProtoMember(9)]
    [JsonPropertyName("aaNLANormR")]
    public string AANonLatinAliasNormalizedReversed { get; set; } = null!;

    [ProtoMember(10)]
    [JsonPropertyName("main")]
    public bool IsMain { get; set; }
}
