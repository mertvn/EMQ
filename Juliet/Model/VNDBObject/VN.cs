using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

public class VN
{
    /// only available with POST /release
    [JsonPropertyName("rtype")]
    public string RType { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; }  = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}
