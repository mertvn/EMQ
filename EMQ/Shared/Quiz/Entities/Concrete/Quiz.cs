using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;
using EMQ.Shared.Auth.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public sealed class Quiz : IDisposable
{
    public Quiz(Room room, int id)
    {
        Room = room;
        Id = id;
    }

    [JsonIgnore]
    public List<QuizLog> QuizLog = new(); // todo move to room

    public const float TickRate = 17;

    public bool IsDisposed;

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
    public Queue<Session> JoinQueue { get; set; } = new(); // todo implement spectating and remove/repurpose

    [JsonIgnore]
    public Dictionary<string, List<Title>> ValidSourcesForLooting { get; set; } = new();

    public void Log(string message, int playerId = -1, bool isSystemMessage = false)
    {
        var quizLog = new QuizLog(Room.Id, Id, Room.QuizSettings, QuizState, playerId, message);
        QuizLog.Add(quizLog);

        Console.WriteLine(quizLog.ToString());

        if (isSystemMessage)
        {
            Room.Chat.Enqueue(new ChatMessage(message));
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        Timer.Stop();
        Timer.Dispose();
    }
}

// todo move to room
public class QuizLog
{
    public QuizLog(int roomId, int quizId, QuizSettings quizSettings, QuizState quizState, int playerId, string message)
    {
        DateTime = DateTime.UtcNow;

        RoomId = roomId;
        QuizId = quizId;
        // QuizSettings = quizSettings;
        QuizState = JsonSerializer.Deserialize<QuizState>(JsonSerializer.Serialize(quizState))!;
        PlayerId = playerId;
        Message = message;
    }

    public DateTime DateTime { get; set; }

    public int RoomId { get; set; }

    public int QuizId { get; set; }

    // public QuizSettings QuizSettings { get; set; }

    public QuizState QuizState { get; set; }

    public int PlayerId { get; set; }

    public string Message { get; set; }

    public override string ToString()
    {
        string final = $"r{RoomId}q{QuizId}@{QuizState.sp} p{PlayerId} {Message}";
        return final;
    }
}
