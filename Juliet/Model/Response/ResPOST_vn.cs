using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResPOST_vn
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}
