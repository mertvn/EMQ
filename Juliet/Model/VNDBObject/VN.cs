using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class VN
{
    // yorhel pls
    // [JsonPropertyName("id")]
    // public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}
