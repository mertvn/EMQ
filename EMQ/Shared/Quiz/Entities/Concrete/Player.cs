using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

    public PlayerStatus PlayerStatus { get; set; }

    public int TeamId { get; set; }

    public int Lives { get; set; }

    public bool IsBuffered { get; set; }

    public PlayerLootingInfo LootingInfo { get; set; } = new();

    public bool IsSkipping { get; set; }

    public PlayerPreferences Preferences { get; set; } = new();
}

public record PlayerLootingInfo
{
    // todo convert to int
    public float X { get; set; }

    public float Y { get; set; }

    public Point TreasureRoomCoords { get; set; } = new();

    public List<Treasure> Inventory { get; set; } = new();
}

public class PlayerPreferences
{
    [Required]
    public bool WantsVideo { get; set; } = true;

    [Required]
    public SongLinkType LinkHost { get; set; } = SongLinkType.Catbox;

    [Required]
    public int VolumeMaster { get; set; } = 70; // todo

    [Required]
    public bool AutoSkipGuessPhase { get; set; } = false; // todo

    [Required]
    public bool AutoSkipResultsPhase { get; set; } = false; // todo

    [Required]
    public bool RestartSongsOnResultsPhase { get; set; } = false; // todo

    // todo language preferences
}
