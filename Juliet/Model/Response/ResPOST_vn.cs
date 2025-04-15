using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResPOST_vn
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("alttitle")]
    public string? AltTitle { get; set; }

    [JsonPropertyName("released")]
    public string? Released { get; set; }

    [JsonPropertyName("olang")]
    public string OLang { get; set; } = "";

    [JsonPropertyName("average")]
    public float Average { get; set; }

    [JsonPropertyName("rating")]
    public float Rating { get; set; }

    [JsonPropertyName("votecount")]
    public int VoteCount { get; set; }

    [JsonPropertyName("developers")]
    public List<Producer> Developers { get; set; } = new();

    [JsonPropertyName("titles")]
    public List<Titles> Titles { get; set; } = new();

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = new();

    [JsonPropertyName("extlinks")]
    public List<Extlinks> Extlinks { get; set; } = new();
}
