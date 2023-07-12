using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResPOST_release
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("alttitle")]
    public string? AltTitle { get; set; }

    [JsonPropertyName("released")]
    public string? Released { get; set; }

    [JsonPropertyName("producers")]
    public List<Producer> Producers { get; set; } = new();

    [JsonPropertyName("vns")]
    public List<VN> VNs { get; set; } = new();
}
