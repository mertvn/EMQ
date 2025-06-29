using System.Text.Json.Serialization;
using EMQ.Shared.Quiz.Entities.Concrete;
using ProtoBuf;

namespace EMQ.Shared.Library.Entities.Concrete;

[ProtoContract]
public class AutocompleteMst
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public AutocompleteMst()
    {
    }

    [JsonConstructor]
    public AutocompleteMst(int msId, string mstLatinTitle, string mstNonLatinTitle = "",
        string mstLatinTitleNormalized = "", string mstNonLatinTitleNormalized = "",
        SongSourceType songSourceType = SongSourceType.Unknown)
    {
        MSId = msId;
        MSTLatinTitle = mstLatinTitle;
        MSTNonLatinTitle = mstNonLatinTitle;
        MSTLatinTitleNormalized = mstLatinTitleNormalized;
        MSTNonLatinTitleNormalized = mstNonLatinTitleNormalized;
        SongSourceType = songSourceType;
    }

    [ProtoMember(1)]
    [JsonPropertyName("1")]
    public int MSId { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("2")]
    public string MSTLatinTitle { get; set; } = null!;

    [ProtoMember(3)]
    [JsonPropertyName("3")]
    public string MSTNonLatinTitle { get; set; } = null!;

    // [ProtoMember(4)]
    [JsonPropertyName("4")]
    public string? MSTLatinTitleNormalized { get; set; }

    // [ProtoMember(5)]
    [JsonPropertyName("5")]
    public string? MSTNonLatinTitleNormalized { get; set; }

    // [ProtoMember(6)]
    [JsonPropertyName("6")]
    public SongSourceType SongSourceType { get; set; }
}
