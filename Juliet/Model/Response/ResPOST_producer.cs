using System.Text.Json.Serialization;

namespace Juliet.Model.Response;

public class ResPOST_producer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("original")]
    public string? Original { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = new();

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "";

    /// "co" for company, "in" for individual, and "ng" for amateur group
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
