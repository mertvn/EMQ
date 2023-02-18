using System.Text.Json.Serialization;

namespace Juliet.Model.Response;

// ReSharper disable once ClassNeverInstantiated.Global
public class ResPOST<T>
{
    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = new();

    [JsonPropertyName("more")]
    public bool More { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("compact_filters")]
    public string? CompactFilters { get; set; }

    [JsonPropertyName("normalized_filters")]
    public List<object>? NormalizedFilters { get; set; }
}
