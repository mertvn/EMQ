using System;
using System.Text.Json.Serialization;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Shared.Auth.Entities.Concrete;

public class Session
{
    public Session(Player player, string token, UserRoleKind userRoleKind, string? activeUserLabelPresetName)
    {
        Player = player;
        Token = token;
        UserRoleKind = userRoleKind;
        ActiveUserLabelPresetName = activeUserLabelPresetName;
        CreatedAt = DateTime.UtcNow;
    }

    public Player Player { get; }

    public string Token { get; set; }

    public string? ConnectionId { get; set; }

    [JsonIgnore]
    // Only available client-side.
    public HubConnection? hubConnection { get; set; }

    public DateTime CreatedAt { get; }

    public UserRoleKind UserRoleKind { get; }

    public string? ActiveUserLabelPresetName { get; set; }
}
