using System;
using System.Text.Json.Serialization;
using EMQ.Shared.Core.SharedDbEntities;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ChatMessage
{
    // needed to be able to keep the timestamp
    [JsonConstructor]
    public ChatMessage(string contents, DateTime timestamp, ChatMessageSender? sender = null)
    {
        Contents = contents;
        Timestamp = timestamp;
        Sender = sender;
    }

    public ChatMessage(string contents, Player? sender = null)
    {
        Contents = contents;
        Sender = sender != null ? new ChatMessageSender(sender.Id, sender.Username, sender.DonorBenefit) : null;
        Timestamp = DateTime.UtcNow;
    }

    public string Contents { get; }

    public ChatMessageSender? Sender { get; }

    public DateTime Timestamp { get; }
}

public class ChatMessageSender
{
    public ChatMessageSender(int userId, string username, DonorBenefit donorBenefit)
    {
        UserId = userId;
        Username = username;
        DonorBenefit = donorBenefit;
    }

    public int UserId { get; }

    public string Username { get; }

    public DonorBenefit DonorBenefit { get; }
}
