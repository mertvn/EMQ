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

    private List<QuizLog> QuizLog = new();

    public const float TickRate = 17;

    public int Id { get; }

    // [JsonIgnore] public Guid Guid { get; set; } = Guid.NewGuid();

    public QuizState QuizState { get; set; } = new();

    [JsonIgnore]
    public Room Room { get; }

    [JsonIgnore]
    public Timer Timer { get; set; } = new();

    [JsonIgnore]
    public List<Song> Songs { get; set; } = new();

    [JsonIgnore]
    public Queue<Session> JoinQueue { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, List<Title>> ValidSources { get; set; } = new();

    public void Log(string message, int playerId = -1)
    {
        var quizLog = new QuizLog(Id, QuizState, playerId, message);
        QuizLog.Add(quizLog);

        Console.WriteLine(quizLog.ToString());
    }
}

public class QuizLog
{
    public QuizLog(int quizId, QuizState quizState, int playerId, string message)
    {
        QuizId = quizId;
        QuizState = quizState;
        PlayerId = playerId;
        Message = message;

        DateTime = DateTime.Now;
    }

    public int QuizId { get; set; }

    public QuizState QuizState { get; set; }

    public int PlayerId { get; set; }

    public string Message { get; set; }

    public DateTime DateTime { get; set; }

    public override string ToString()
    {
        string final = $"q{QuizId}@{QuizState.sp} {PlayerId} {Message}";
        return final;
    }
}
