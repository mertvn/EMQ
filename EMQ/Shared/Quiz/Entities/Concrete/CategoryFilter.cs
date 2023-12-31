using System.Text.Json.Serialization;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class CategoryFilter
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public CategoryFilter()
    {
    }

    [JsonConstructor]
    public CategoryFilter(SongSourceCategory songSourceCategory, LabelKind trilean)
    {
        SongSourceCategory = songSourceCategory;
        Trilean = trilean;
    }

    [ProtoMember(1)]
    public SongSourceCategory SongSourceCategory { get; } = null!;

    [ProtoMember(2)]
    public LabelKind Trilean { get; set; } // todo actual trilean type
}
