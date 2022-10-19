using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorApp1.Shared.Auth;

// todo: should be split into two: Persist and Session
public class Session
{
    public Session(int playerId, string token)
    {
        PlayerId = playerId;
        Token = token;
    }

    public int PlayerId { get; }

    public string Token { get; }

    public string? ConnectionId { get; set; }

    [JsonIgnore] public HubConnection? hubConnection { get; set; }

    public int? RoomId { get; set; }
}
