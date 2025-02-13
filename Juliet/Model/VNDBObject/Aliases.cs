using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class Aliases
{
    [JsonPropertyName("aid")]
    public int Aid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("latin")]
    public string Latin { get; set; } = "";

    [JsonPropertyName("ismain")]
    public bool IsMain { get; set; }
}
