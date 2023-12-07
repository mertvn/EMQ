using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public Timer Timer { get; set; } = new();

    [JsonIgnore]
    public List<Song> Songs { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, List<Title>> ValidSourcesForLooting { get; set; } = new();

    public Dictionary<int, List<Title>> MultipleChoiceOptions { get; set; } = new();

    public void Dispose()
    {
        IsDisposed = true;
        Timer.Stop();
        Timer.Dispose();
    }
}
