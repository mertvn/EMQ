using System.Text.Json.Serialization;
using EMQ.Shared.Library.Entities.Concrete;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract(UseProtoMembersOnly = true)]
public class ArtistFilter
{
    // /// only for protobuf, do not use
    // // ReSharper disable once UnusedMember.Global
    public ArtistFilter()
    {
    }

    [JsonConstructor]
    public ArtistFilter(AutocompleteA artist, LabelKind trilean)
    {
        Artist = artist;
        Trilean = trilean;
    }

    [ProtoMember(1)]
    public AutocompleteA Artist { get; } = null!;

    [ProtoMember(2)]
    public LabelKind Trilean { get; set; } // todo actual trilean type
}
