using System;
using System.Text.Json;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class RoomLog
{
    public RoomLog(Guid roomId, Guid quizId, QuizSettings quizSettings, QuizState? quizState, int playerId,
        string message)
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

    public Guid RoomId { get; set; }

    public Guid QuizId { get; set; }

    // public QuizSettings QuizSettings { get; set; }

    public QuizState? QuizState { get; set; }

    public int PlayerId { get; set; }

    public string Message { get; set; }

    public override string ToString()
    {
        string final = $"r{RoomId}q{QuizId}@{QuizState?.sp ?? -1} p{PlayerId} {Message}";
        return final;
    }
}
