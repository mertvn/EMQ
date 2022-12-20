using System.Text.Json.Serialization;

namespace Juliet.Model.VNDBObject;

// ReSharper disable once ClassNeverInstantiated.Global
public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("lengthvotes")]
    public int? LengthVotes { get; set; }

    [JsonPropertyName("lengthvotes_sum")]
    public int? LengthVotesSum { get; set; }
}
