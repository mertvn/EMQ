using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResPOST_ulist : ResPOST<ResPOST_ulist>
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("added")]
    public int? Added { get; set; }

    [JsonPropertyName("vote")]
    public int? Vote { get; set; }

    [JsonPropertyName("vn")]
    public VN VN { get; set; } = new();
}
