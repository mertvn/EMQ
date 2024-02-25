using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Timers;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public sealed class Quiz : IDisposable
{
    public Quiz(Room room, Guid id)
    {
        Room = room;
        Id = id;
    }

    public const float TickRate = 17;

    public bool IsDisposed;

    public Guid Id { get; }

    public QuizState QuizState { get; set; } = new();

    [JsonIgnore]
    public Room Room { get; }

    [JsonIgnore]
    public PeriodicTimer? Timer { get; set; }

    public bool IsTimerRunning { get; set; }

    [JsonIgnore]
    public List<Song> Songs { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, List<Title>> ValidSourcesForLooting { get; set; } = new();

    public Dictionary<int, List<Title>> MultipleChoiceOptions { get; set; } = new(); // todo move into QuizState

    [JsonIgnore]
    public Dictionary<int, SongHistory> SongsHistory { get; set; } = new();

    public void Dispose()
    {
        IsDisposed = true;
        IsTimerRunning = false;
        Timer?.Dispose();
    }
}

public class SongHistory
{
    public Song Song { get; set; } = new();

    public Dictionary<int, GuessInfo> PlayerGuessInfos { get; set; } = new();

    public long TimesCorrect => PlayerGuessInfos.Count(x => x.Value.IsGuessCorrect);

    public long TimesPlayed => PlayerGuessInfos.Count;

    // public float CorrectPercentage => PlayerGuessInfos.Count(x => todo);

    public long TimesGuessed => PlayerGuessInfos.Count(x => !string.IsNullOrWhiteSpace(x.Value.Guess));

    public long TotalGuessMs => PlayerGuessInfos.Sum(x => x.Value.FirstGuessMs);

    // public int AverageGuessMs => PlayerGuessInfos.Count(x => todo);

    public static SongStats ToSongStats(SongHistory songHistory)
    {
        return new SongStats
        {
            TimesCorrect = songHistory.TimesCorrect,
            TimesPlayed = songHistory.TimesPlayed,
            TimesGuessed = songHistory.TimesGuessed,
            TotalGuessMs = songHistory.TotalGuessMs,
        };
    }
}

public readonly struct GuessInfo
{
    public string Username { get; init; }

    public string Guess { get; init; }

    public int FirstGuessMs { get; init; }

    public bool IsGuessCorrect { get; init; }

    public List<Label>? Labels { get; init; }
}
