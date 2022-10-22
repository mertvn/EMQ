using System.Text.Json.Serialization;
using BlazorApp1.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorApp1.Shared.Auth.Entities.Concrete;

// todo: should be split into two: Persist and Session
public class Session
{
    public Session(Player player, string token)
    {
        Player = player;
        Token = token;
    }

    public Player Player { get; }

    public string Token { get; }

    public string? ConnectionId { get; set; }

    [JsonIgnore] public HubConnection? hubConnection { get; set; }

    public int? RoomId { get; set; }
}
