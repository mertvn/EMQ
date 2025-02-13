using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class Extlinks
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}
