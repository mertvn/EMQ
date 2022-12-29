using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Timers;
using EMQ.Shared.Auth.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Quiz
{
    public Quiz(Room room, int id)
    {
        Room = room;
        Id = id;
    }

    public int Id { get; }

    // [JsonIgnore] public Guid Guid { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public Room Room { get; }

    [JsonIgnore]
    public Timer Timer { get; set; } = new();

    public const float TickRate = 17;

    public QuizState QuizState { get; set; } = new();

    [JsonIgnore]
    public List<Song> Songs { get; set; } = new();

    public Queue<Session> JoinQueue { get; set; } = new();
}
