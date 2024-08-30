using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class TreasureRoomComponent
{
    [Parameter]
    public Room? Room { get; set; }

    [Parameter]
    public float Countdown { get; set; }

    private ElementReference _treasureRoomMainDivRef;

    private Timer _movementTimer = new() { Interval = Quiz.TickRate };

    private DateTime LastReport { get; set; }

    private bool IsSpectator => Room?.Spectators.Any(x => x.Id == ClientState.Session?.Player.Id) ?? false;

    private Dictionary<string, bool> Keys { get; set; } = new()
    {
        { "arrowup", false },
        { "arrowdown", false },
        { "arrowleft", false },
        { "arrowright", false },
        { "w", false },
        { "s", false },
        { "a", false },
        { "d", false },
    };

    protected override async Task OnInitializedAsync()
    {
        _movementTimer.Elapsed += MovementTimerOnElapsed;
        _movementTimer.Start();
    }

    private void MovementTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        // todo? fix diagonal movement being faster
        const int speed = 4; // todo make this a quiz setting?

        Player player = Room!.Players.SingleOrDefault(x => x.Id == ClientState.Session!.Player.Id) ??
                        Room!.Spectators.Single(x => x.Id == ClientState.Session!.Player.Id);

        var newX = player.LootingInfo.X;
        var newY = player.LootingInfo.Y;

        var moved = false;

        if (Keys["arrowup"] || Keys["w"])
        {
            newY -= speed;
            moved = true;
        }

        if (Keys["arrowdown"] || Keys["s"])
        {
            newY += speed;
            moved = true;
        }

        if (Keys["arrowleft"] || Keys["a"])
        {
            newX -= speed;
            moved = true;
        }

        if (Keys["arrowright"] || Keys["d"])
        {
            newX += speed;
            moved = true;
        }

        if (moved)
        {
            player.LootingInfo.X = Math.Clamp(newX, 0,
                LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize);

            player.LootingInfo.Y = Math.Clamp(newY, 0,
                LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize);

            StateHasChanged();

            if ((DateTime.UtcNow - LastReport) > TimeSpan.FromMilliseconds(Quiz.TickRateClient))
            {
                LastReport = DateTime.UtcNow;
                // can't afford to wait for this call
#pragma warning disable CS4014
                ReportPositionToServer(newX, newY);
#pragma warning restore CS4014
            }
        }
    }

    private async Task ReportPositionToServer(int newX, int newY)
    {
        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerMoved", newX, newY, DateTime.UtcNow);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _treasureRoomMainDivRef.FocusAsync();
        }
    }

    private async Task PickupTreasure(Treasure treasure)
    {
        Player player = Room!.Players.SingleOrDefault(x => x.Id == ClientState.Session!.Player.Id) ??
                        Room!.Spectators.Single(x => x.Id == ClientState.Session!.Player.Id);

        if (treasure.Position.IsReachableFromCoords(player.LootingInfo.X, player.LootingInfo.Y))
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendPickupTreasure", treasure.Guid);
        }
    }

    private async Task DropTreasure(Treasure treasure)
    {
        await ClientState.Session!.hubConnection!.SendAsync("SendDropTreasure", treasure.Guid);
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        // Console.WriteLine(e.Key);
        if (Keys.ContainsKey(e.Key.ToLowerInvariant()))
        {
            Keys[e.Key.ToLowerInvariant()] = true;
        }
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        // Console.WriteLine(e.Key);
        if (Keys.ContainsKey(e.Key.ToLowerInvariant()))
        {
            Keys[e.Key.ToLowerInvariant()] = false;
        }
    }

    private async Task OnclickChangeTreasureRoomArrow(Point arrowPosition, Point treasureRoomCoords,
        Direction direction)
    {
        // todo fix teleporting if you move while changing rooms
        Player player = Room!.Players.SingleOrDefault(x => x.Id == ClientState.Session!.Player.Id) ??
                        Room!.Spectators.Single(x => x.Id == ClientState.Session!.Player.Id);

        if (IsSpectator || arrowPosition.IsReachableFromCoords(player.LootingInfo.X, player.LootingInfo.Y))
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendChangeTreasureRoom", treasureRoomCoords,
                direction);
        }
    }

    public void CallStateHasChanged(Room room)
    {
        Room = room;
        StateHasChanged();
    }

    public void StopTimers()
    {
        _movementTimer.Stop();
        _movementTimer.Elapsed -= MovementTimerOnElapsed;
    }
}
