using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Client.Components;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;

namespace EMQ.Client.Pages;

public partial class PyramidPage
{
    public PyramidPage()
    {
        _handlers = new()
        {
            {
                "ReceiveUpdatePlayerLootingInfo", (new Type[] { typeof(int), typeof(PlayerLootingInfo), typeof(bool) },
                    async param =>
                    {
                        await OnReceiveUpdatePlayerLootingInfo((int)param[0]!, (PlayerLootingInfo)param[1]!,
                            (bool)param[2]!);
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

    private Room? Room { get; set; }

    private readonly Dictionary<string, (Type[] types, Func<object?[], Task> value)> _handlers;

    private TreasureRoomComponent _treasureRoomComponentRef = null!;

    public Timer Timer { get; } = new();

    public float Countdown { get; set; }

    private IDisposable? _locationChangingRegistration;

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        if (ClientState.Session is null) // todo check if user !belongs to a quiz as well
        {
            _locationChangingRegistration?.Dispose();
            _navigation.NavigateTo("/", true);
            return;
        }

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

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _locationChangingRegistration = _navigation.RegisterLocationChangingHandler(OnLocationChanging);
        }
    }

    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (context.TargetLocation is not "/QuizPage")
        {
            context.PreventNavigation();
        }
    }

    private async Task SyncWithServer()
    {
        Room = await _clientUtils.SyncRoom();
        // Console.WriteLine(JsonSerializer.Serialize(Room, Utils.JsoIndented));
        Countdown = Room!.Quiz!.QuizState.RemainingMs;
        StateHasChanged();
        _treasureRoomComponentRef.CallStateHasChanged(Room);
    }

    private async Task OnReceiveUpdatePlayerLootingInfo(int playerId, PlayerLootingInfo playerLootingInfo,
        bool shouldUpdatePosition)
    {
        if (Room != null)
        {
            Player player = Room.Players.Single(x => x.Id == playerId);

            player.LootingInfo.TreasureRoomCoords = playerLootingInfo.TreasureRoomCoords;
            if (playerId == ClientState.Session!.Player.Id)
            {
                player.LootingInfo.Inventory = playerLootingInfo.Inventory;
                if (shouldUpdatePosition)
                {
                    player.LootingInfo.X = playerLootingInfo.X;
                    player.LootingInfo.Y = playerLootingInfo.Y;
                }
            }
            else
            {
                // we are not updating inventory here because it should be private (server shouldn't sending it anyways but just in case)
                player.LootingInfo.X = playerLootingInfo.X;
                player.LootingInfo.Y = playerLootingInfo.Y;
            }

            StateHasChanged();
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

    private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (Countdown > 0)
        {
            Countdown -= Quiz.TickRate;
        }
        else
        {
            Timer.Stop();
            Timer.Elapsed -= OnTimedEvent;

            await SyncWithServer();
            if (Countdown <= 0 || Room!.Quiz!.QuizState.Phase != QuizPhaseKind.Looting)
            {
                await OnReceiveQuizEntered();
            }
        }

        StateHasChanged();
        // if (Room != null)
        // {
        //     _treasureRoomComponentRef.CallStateHasChanged(Room);
        // }
    }

    private async Task OnReceiveQuizEntered()
    {
        // Double-entering QuizPage can trigger the Leave Quiz modal to pop up for some reason
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        if (_navigation.Uri.EndsWith("/QuizPage"))
        {
            return;
        }

        _treasureRoomComponentRef.StopTimers();

        _locationChangingRegistration?.Dispose();
        _logger.LogInformation("Navigating from Pyramid to Quiz");
        _navigation.NavigateTo("/QuizPage");
    }

    private async Task OnReceiveUpdateRemainingMs(float remainingMs)
    {
        Countdown = remainingMs;
    }
}
