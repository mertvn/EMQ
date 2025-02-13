using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResPOST_staff
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("aid")]
    public int Aid { get; set; }

    [JsonPropertyName("ismain")]
    public bool IsMain { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("original")]
    public string Original { get; set; } = "";

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "";

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("extlinks")]
    public List<Extlinks> Extlinks { get; set; } = new();

    [JsonPropertyName("aliases")]
    public List<Aliases> Aliases { get; set; } = new();
}
