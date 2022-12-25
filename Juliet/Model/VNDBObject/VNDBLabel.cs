using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class VNDBLabel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
