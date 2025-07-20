using System.Text.Json.Serialization;
using EMQ.Shared.Quiz.Entities.Concrete;
using ProtoBuf;

namespace EMQ.Shared.Library.Entities.Concrete;

[ProtoContract]
public class AutocompleteCollection
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public AutocompleteCollection()
    {
    }

    [JsonConstructor]
    public AutocompleteCollection(int coId, string name)
    {
        CoId = coId;
        Name = name;
    }

    [ProtoMember(1)]
    [JsonPropertyName("1")]
    public int CoId { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("2")]
    public string Name { get; set; } = null!;
}
