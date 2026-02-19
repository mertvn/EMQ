using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Player
{
    public Player(int id, string username, Avatar avatar)
    {
        Id = id;
        Username = username;
        Avatar = avatar;
    }

    public int Id { get; }

    public string Username { get; set; }

    // public string DisplayName { get; }

    [JsonIgnore]
    public PlayerGuess? Guess { get; set; }

    public int Score { get; set; }

    public Avatar Avatar { get; set; }

    public PlayerStatus PlayerStatus { get; set; }

    public int TeamId { get; set; } = 1;

    public int Lives { get; set; }

    public bool IsBuffered { get; set; }

    public PlayerLootingInfo LootingInfo { get; set; } = new(); // todo null if not looting

    public bool IsSkipping { get; set; }

    // do not rename to IsReady
    public bool IsReadiedUp { get; set; }

    public DateTime LastHeartbeatTimestamp { get; set; }

    public DateTime LastHeartbeatTimestampQuiz { get; set; }

    public bool HasActiveConnection =>
        IsBot || (DateTime.UtcNow - LastHeartbeatTimestamp) < TimeSpan.FromSeconds(30);

    public bool HasActiveConnectionQuiz =>
        IsBot || (DateTime.UtcNow - LastHeartbeatTimestampQuiz) < TimeSpan.FromSeconds(30);

    public int NGMCGuessesInitial { get; set; }

    public float NGMCGuessesCurrent { get; set; }

    public bool NGMCCanBurn { get; set; }

    public bool NGMCCanBePicked { get; set; }

    public bool NGMCMustPick { get; set; }

    public bool NGMCMustBurn { get; set; }

    public Dictionary<GuessKind, bool?>? IsGuessKindCorrectDict { get; set; }

    public AnsweringKind AnsweringKind { get; set; }

    public PlayerBotInfo? BotInfo { get; set; }

    public bool IsBot => BotInfo != null;

    [JsonIgnore]
    public Dictionary<int, DateTime> SongLastPlayedAtDict { get; } = new();

    [JsonIgnore]
    public Dictionary<string, DateTime> VNLastPlayedAtDict { get; } = new();

    public DonorBenefit DonorBenefit { get; set; } = new();
}

public class PlayerConnectionInfo
{
    public DateTime LastHeartbeatTimestamp { get; set; }

    public string Page { get; set; } = ""; // todo? enum
}

public class PlayerBotInfo
{
    public string VndbId { get; set; } = "";

    public SongDifficultyLevel Difficulty { get; set; } = SongDifficultyLevel.Medium; // todo? different difficulty type

    public PlayerBotKind BotKind { get; set; }

    public string MimickedUsername { get; set; } = "";

    public float LastSongHitChance { get; set; }

    public Dictionary<int, Dictionary<GuessKind, float>?> SongHitChanceDict { get; } = new();
}

public enum PlayerBotKind
{
    Default,
    Mimic,
}

public class PlayerGuess
{
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public Dictionary<GuessKind, string?> Dict { get; set; } = new()
    {
        { GuessKind.Mst, null },
        { GuessKind.A, null },
        { GuessKind.Mt, null },
        { GuessKind.Rigger, null },
        { GuessKind.Developer, null },
        { GuessKind.Composer, null },
        { GuessKind.Arranger, null },
        { GuessKind.Lyricist, null },
        { GuessKind.Character, null },
        { GuessKind.Illustrator, null },
        { GuessKind.Seiyuu, null },
    };

    public Dictionary<GuessKind, int> DictFirstGuessMs { get; set; } = new()
    {
        { GuessKind.Mst, 0 },
        { GuessKind.A, 0 },
        { GuessKind.Mt, 0 },
        { GuessKind.Rigger, 0 },
        { GuessKind.Developer, 0 },
        { GuessKind.Composer, 0 },
        { GuessKind.Arranger, 0 },
        { GuessKind.Lyricist, 0 },
        { GuessKind.Character, 0 },
        { GuessKind.Illustrator, 0 },
        { GuessKind.Seiyuu, 0 },
    };

    public override string ToString()
    {
        string ret = "";
        foreach ((GuessKind key, string? value) in Dict)
        {
            if (key == GuessKind.Mst)
            {
                ret = value ?? "";
            }
            else
            {
                if (value != null)
                {
                    ret += $" {key.ToString()}: {value}";
                }
            }
        }

        return ret;
    }
}

public record PlayerLootingInfo
{
    public int X { get; set; }

    public int Y { get; set; }

    public Point TreasureRoomCoords { get; set; } = new();

    public List<Treasure> Inventory { get; set; } = new();
}

public class PlayerPreferences
{
    [Required]
    public bool WantsVideo { get; set; } = true;

    [Required]
    public SongLinkType LinkHost { get; set; } = SongLinkType.Self;

    [Required]
    public int VolumeMaster { get; set; } = 70;

    // todo make these skip preferences not cause unskip
    [Required]
    public bool AutoSkipGuessPhase { get; set; } = false;

    [Required]
    public bool AutoSkipResultsPhase { get; set; } = false;

    [Required]
    public bool RestartSongsOnResultsPhase { get; set; } = false;

    [Required]
    public bool HideVideo { get; set; } = false;

    [Required]
    public bool WantsEnglish { get; set; } = false;

    [Required]
    public bool WantsOriginalTitle { get; set; } = false;

    [Required]
    public bool ShowVndbCovers { get; set; } = true;

    [Required]
    public bool ShowSpacedRepetitionInfo { get; set; } = false;

    // todo only hide spoilers if not finished/voted
    [Required]
    public bool HideSpoilers { get; set; } = true;

    [Required]
    public bool HideFlashingLights { get; set; } = true;

    [Required]
    public bool SwapArtistNameAndSongTitleDropdowns { get; set; } = false;

    [Required]
    public bool ForceDefaultAvatar { get; set; } = false;

    [Required]
    public bool DebugMode { get; set; } = false;

    [Required]
    public bool AutocompleteHighlightMatch { get; set; } = true;

    [Required]
    public bool AutocompleteRequireConfirmation { get; set; } = false;

    [Required]
    public bool ShowSnowflakes { get; set; } = true;

    [Required]
    public bool MuteWhenDuca { get; set; } = false;

    [Required]
    public bool AutocompleteShowIcons { get; set; } = true;

    [Required]
    public bool AutocompleteIsEnabled { get; set; } = true;

    [Required]
    public CdnEdgeKind CdnEdge { get; set; } = CdnEdgeKind.NaEast;
}

public enum CdnEdgeKind
{
    Auto,

    [Description("NA-East (Virginia)")]
    NaEast,

    [Description("Asia (Singapore)")]
    Asia,
}
