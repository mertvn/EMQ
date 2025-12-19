using System.Text.Json.Serialization;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class SongDifficultyFilter
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public SongDifficultyFilter()
    {
    }

    [JsonConstructor]
    public SongDifficultyFilter(int min, int max)
    {
        Min = min;
        Max = max;
    }

    [ProtoMember(1)]
    public int Min { get; set; }

    [ProtoMember(2)]
    public int Max { get; set; }
}
