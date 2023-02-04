using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ChatMessage
{
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
