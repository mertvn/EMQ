using System;
using System.Text.Json.Serialization;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Shared.Auth.Entities.Concrete;

// todo: should be split into two: Persist and Session
public class Session
{
    public Session(Player player, string token)
    {
        Player = player;
        Token = token;
        CreatedAt = DateTime.Now;
    }

    public Player Player { get; }

    public string Token { get; }

    public string? ConnectionId { get; set; }

    [JsonIgnore]
    public HubConnection? hubConnection { get; set; }

    public int? RoomId { get; set; }

    public PlayerVndbInfo VndbInfo { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}
