using System.Text.Json.Serialization;

namespace EMQ.Shared.Core;

public readonly struct TimeRange
{
    public TimeRange(double start, double end)
    {
        Start = start;
        End = end;
    }

    [JsonPropertyName("s")]
    public double Start { get; }

    [JsonPropertyName("e")]
    public double End { get; }

    [JsonIgnore]
    public double Duration => End - Start;

    public override string ToString()
    {
        return $"Start: {Start:F2}s, End: {End:F2}s, Duration: {Duration:F2}s";
    }
}
