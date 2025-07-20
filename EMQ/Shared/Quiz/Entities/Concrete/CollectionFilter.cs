using System.Text.Json.Serialization;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Library.Entities.Concrete;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class CollectionFilter
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public CollectionFilter()
    {
    }

    [JsonConstructor]
    public CollectionFilter(AutocompleteCollection autocompleteCollection, LabelKind trilean)
    {
        AutocompleteCollection = autocompleteCollection;
        Trilean = trilean;
    }

    [ProtoMember(1)]
    public AutocompleteCollection AutocompleteCollection { get; } = null!;

    [ProtoMember(2)]
    public LabelKind Trilean { get; set; } // todo actual trilean type
}
