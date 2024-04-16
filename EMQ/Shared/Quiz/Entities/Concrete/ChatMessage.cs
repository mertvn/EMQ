using System;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ChatMessage
{
    // needed to be able to keep the timestamp
    [JsonConstructor]
    public ChatMessage(string contents, DateTime timestamp, Player? sender = null)
    {
        Contents = contents;
        Timestamp = timestamp;
        Sender = sender;
    }

    public ChatMessage(string contents, Player? sender = null)
    {
        Contents = contents;
        Sender = sender;
        Timestamp = DateTime.UtcNow;
    }

    public string Contents { get; }

    public Player? Sender { get; }

    public DateTime Timestamp { get; }
}
