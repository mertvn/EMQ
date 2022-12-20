using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResGET_authinfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = Array.Empty<string>();
}
