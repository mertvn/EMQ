using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Player
{
    public Player(int id, string username)
    {
        Id = id;
        Username = username;
    }

    public int Id { get; }

    public string Username { get; }

    // public string DisplayName { get; }

    public string Guess { get; set; } = "";

    public int Score { get; set; }

    public Avatar? Avatar { get; set; }

    public PlayerState PlayerState { get; set; }

    public int TeamId { get; set; }

    public int Lives { get; set; }

    public bool IsBuffered { get; set; }

    public PlayerVndbInfo VndbInfo { get; set; } = new();
}

// todo
public class PlayerVndbInfo
{
    public string? VndbId { get; set; }

    [JsonIgnore]
    public string? VndbApiToken { get; set; }

    [JsonIgnore]
    public List<string>? VNs { get; set; }
}
