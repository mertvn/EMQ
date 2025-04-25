using System.Text.Json.Serialization;
using EMQ.Shared.Quiz.Entities.Concrete;
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
        bool isMain = true,
        SongArtistRole mainRole = SongArtistRole.Unknown)
    {
        AId = aId;
        // VndbId = vndbId;
        AALatinAlias = aaLatinAlias;
        AANonLatinAlias = aaNonLatinAlias;
        IsMain = isMain;
        MainRole = mainRole;
        // AAId = aaId;
    }

    [ProtoMember(1)]
    [JsonPropertyName("1")]
    public int AId { get; set; }

    // [ProtoMember(2)]
    // [JsonPropertyName("2")]
    // public string VndbId { get; set; } = null!;

    [ProtoMember(3)]
    [JsonPropertyName("3")]
    public string AALatinAlias { get; set; } = null!;

    [ProtoMember(4)]
    [JsonPropertyName("4")]
    public string AANonLatinAlias { get; set; } = null!;

    // [ProtoMember(5)]
    // [JsonPropertyName("5")]
    // public int AAId { get; set; }

    [ProtoMember(6)]
    [JsonPropertyName("6")]
    public string AALatinAliasNormalized { get; set; } = null!;

    [ProtoMember(7)]
    [JsonPropertyName("7")]
    public string AANonLatinAliasNormalized { get; set; } = null!;

    [ProtoMember(8)]
    [JsonPropertyName("8")]
    public string AALatinAliasNormalizedReversed { get; set; } = null!;

    [ProtoMember(9)]
    [JsonPropertyName("9")]
    public string AANonLatinAliasNormalizedReversed { get; set; } = null!;

    [ProtoMember(10)]
    [JsonPropertyName("10")]
    public bool IsMain { get; set; }

    [ProtoMember(11)]
    [JsonPropertyName("11")]
    public SongArtistRole MainRole { get; set; }
}
