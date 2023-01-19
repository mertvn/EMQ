using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.Extensions.Logging;

namespace EMQ.Client.Pages;

public partial class PyramidPage
{
    public PyramidPage()
    {
        _handlers = new()
        {
            {
                "ReceiveUpdatePlayerLootingInfo", (new Type[] { typeof(int), typeof(PlayerLootingInfo), },
                    async param =>
                    {
                        await OnReceiveUpdatePlayerLootingInfo((int)param[0]!, (PlayerLootingInfo)param[1]!);
                    })
            },
            {
                "ReceiveUpdateTreasureRoom", (new Type[] { typeof(TreasureRoom), },
                    async param => { await OnReceiveUpdateTreasureRoom((TreasureRoom)param[0]!); })
            },
            {
                "ReceiveUpdateRemainingMs",
                (new Type[] { typeof(float) }, async param => { await OnReceiveUpdateRemainingMs((float)param[0]!); })
            },
            { "ReceiveQuizEntered", (new Type[] { }, async _ => { await OnReceiveQuizEntered(); }) },
        };
    }

    private static Room? Room { get; set; }

    private readonly Dictionary<string, (Type[] types, Func<object?[], Task> value)> _handlers;

    private TreasureRoomComponent _treasureRoomComponentRef = null!;

    public Timer Timer { get; } = new();

    public float Countdown { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        await SyncWithServer();
        if (!Room!.TreasureRooms.Any(x => x.Any(y => y.Treasures.Any(z => z.ValidSource.Value.Any()))))
        {
            Console.WriteLine("Resync Pyramid");
            await Task.Delay(TimeSpan.FromSeconds(3));
            await SyncWithServer();
        }

        await _clientConnectionManager.SetHandlers(_handlers);
        SetTimer();
        StateHasChanged();
        _treasureRoomComponentRef.CallStateHasChanged(Room);
    }

    private async Task SyncWithServer()
    {
        Room = await _clientUtils.SyncRoom();
        // Console.WriteLine(JsonSerializer.Serialize(Room, Utils.JsoIndented));
        Countdown = Room!.Quiz!.QuizState.RemainingMs;
        StateHasChanged();
        _treasureRoomComponentRef.CallStateHasChanged(Room);
    }

    private async Task OnReceiveUpdatePlayerLootingInfo(int playerId, PlayerLootingInfo playerLootingInfo)
    {
        if (Room != null)
        {
            Player player = Room.Players.Single(x => x.Id == playerId);
            player.LootingInfo = playerLootingInfo;

            // player.LootingInfo.TreasureRoomId = playerLootingInfo.TreasureRoomId;

            // if (playerId == ClientState.Session!.Player.Id)
            // {
            //     player.LootingInfo.Inventory = playerLootingInfo.Inventory;
            // }
            // else
            // {
            //     player.LootingInfo.X = playerLootingInfo.X;
            //     player.LootingInfo.Y = playerLootingInfo.Y;
            // }

            StateHasChanged();
             // _treasureRoomComponentRef.CallStateHasChanged(Room);
        }
    }

    private async Task OnReceiveUpdateTreasureRoom(TreasureRoom treasureRoom)
    {
        Room!.TreasureRooms[treasureRoom.Coords.X][treasureRoom.Coords.Y] = treasureRoom;
        StateHasChanged();
        _treasureRoomComponentRef.CallStateHasChanged(Room);
    }

    private void SetTimer()
    {
        Timer.Stop();
        Timer.Elapsed -= OnTimedEvent;

        Timer.Interval = TimeSpan.FromMilliseconds(Quiz.TickRate).TotalMilliseconds;
        Timer.Elapsed += OnTimedEvent;
        Timer.AutoReset = true;
        Timer.Start();
    }

    private void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (Countdown > 0)
        {
            Countdown -= Quiz.TickRate;
        }
        else
        {
            Timer.Stop();
            Timer.Elapsed -= OnTimedEvent;
        }

        StateHasChanged();
        // if (Room != null)
        // {
        //     _treasureRoomComponentRef.CallStateHasChanged(Room);
        // }
    }

    private async Task OnReceiveQuizEntered()
    {
        _treasureRoomComponentRef.StopTimers();

        _logger.LogInformation("Navigating from Pyramid to Quiz");
        _navigation.NavigateTo("/QuizPage");
    }

    private async Task OnReceiveUpdateRemainingMs(float remainingMs)
    {
        Countdown = remainingMs;
    }
}
