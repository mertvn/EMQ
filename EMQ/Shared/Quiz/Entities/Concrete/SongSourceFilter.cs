using System.Text.Json.Serialization;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Library.Entities.Concrete;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class SongSourceFilter
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public SongSourceFilter()
    {
    }

    [JsonConstructor]
    public SongSourceFilter(AutocompleteMst autocompleteMst, LabelKind trilean)
    {
        AutocompleteMst = autocompleteMst;
        Trilean = trilean;
    }

    [ProtoMember(1)]
    public AutocompleteMst AutocompleteMst { get; } = null!;

    [ProtoMember(2)]
    public LabelKind Trilean { get; set; } // todo actual trilean type
}
