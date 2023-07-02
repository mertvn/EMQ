using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class Producer
{
    /// only available with POST /release
    [JsonPropertyName("developer")]
    public bool? Developer { get; set; }

    /// only available with POST /release
    [JsonPropertyName("publisher")]
    public bool? Publisher { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("original")]
    public string? Original { get; set; }
}
