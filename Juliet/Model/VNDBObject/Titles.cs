using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class Titles
{
    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("latin")]
    public string Latin { get; set; } = "";

    [JsonPropertyName("official")]
    public bool Official { get; set; }

    [JsonPropertyName("main")]
    public bool Main { get; set; }
}
