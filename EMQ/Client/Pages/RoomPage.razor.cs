﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Client.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Pages;

public partial class RoomPage
{
    public RoomPage()
    {
        _handlers = new Dictionary<string, (Type[] types, Func<object?[], Task> value)>
        {
            { "ReceivePlayerJoinedRoom", (new Type[] { }, async _ => { await OnReceivePlayerJoinedRoom(); }) },
            { "ReceiveQuizEntered", (new Type[] { }, async _ => { await OnReceiveQuizEntered(); }) },
            { "ReceivePyramidEntered", (new Type[] { }, async _ => { await OnReceivePyramidEntered(); }) },
            {
                "ReceiveUpdateRoomForRoom", (new Type[] { typeof(Room) },
                    async param => { await OnReceiveUpdateRoomForRoom((Room)param[0]!); })
            },
            { "ReceiveKickedFromRoom", (new Type[] { }, async _ => { await OnReceiveKickedFromRoom(); }) },
        };
    }

    private Room? Room { get; set; }

    private readonly Dictionary<string, (Type[] types, Func<object?[], Task> value)> _handlers;

    public bool IsStartingQuiz { get; set; }

    private ChatComponent? _chatComponent;

    private QuizSettingsComponent? _quizSettingsComponent;

    private GenericModal? _leaveModalRef;

    private GenericModal? _forceStartModalRef;

    private GenericModal? _changeRoomNameAndPasswordModalRef;

    private SongHistoryWrapperComponent? _songHistoryWrapperComponent;

    private string? RoomName { get; set; }

    private string? RoomPassword { get; set; }

    private IDisposable? _locationChangingRegistration;

    private bool IsSpectator => Room?.Spectators.Any(x => x.Id == ClientState.Session?.Player.Id) ?? false;

    private string InviteLinkButtonText { get; set; } = "Invite link";

    private Dictionary<int, SongHistory> ClientSongsHistory { get; set; } = new();

    private async Task SetSelectedTeamIdAsync(int value, int userId)
    {
        var req = new ReqSetTeamId(value, userId);
        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/SetTeamId", req);
        if (res1.IsSuccessStatusCode)
        {
            Room = await _clientUtils.SyncRoom();
            StateHasChanged();
        }
    }

    private async Task SetSelectedNGMCGuessesInitialAsync(int value, int userId)
    {
        var req = new ReqSetTeamId(value, userId);
        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/SetNGMCGuessesInitial", req);
        if (res1.IsSuccessStatusCode)
        {
            Room = await _clientUtils.SyncRoom();
            StateHasChanged();
        }
    }

    private async Task SetAnsweringKindAsync(int value)
    {
        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/SetAnsweringKind", value);
        if (res1.IsSuccessStatusCode)
        {
            Room = await _clientUtils.SyncRoom();
            StateHasChanged();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        await ClientUtils.SendPong(_navigation.Uri.LastSegment());

        var room = await _clientUtils.SyncRoom();
        if (ClientState.Session is null || room is null)
        {
            _locationChangingRegistration?.Dispose();
            _navigation.NavigateTo("/", true);
            return;
        }

        await _clientConnectionManager.SetHandlers(_handlers);

        Room = await _clientUtils.SyncRoom();
        if (Room != null)
        {
            StateHasChanged();
        }
        else
        {
            // todo require reload etc.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _locationChangingRegistration = _navigation.RegisterLocationChangingHandler(OnLocationChanging);
        }

        if (Room is { Quiz.QuizState.QuizStatus: QuizStatus.Playing })
        {
            if (Room.Quiz.QuizState.Phase == QuizPhaseKind.Looting)
            {
                await OnReceivePyramidEntered();
            }
            else
            {
                await OnReceiveQuizEntered();
            }
        }
    }

    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (context.TargetLocation is not "/QuizPage" or "/PyramidPage")
        {
            context.PreventNavigation();
            _leaveModalRef?.Show();
        }
    }

    private async Task StartQuiz()
    {
        while (ClientState.Session?.hubConnection?.State is not HubConnectionState.Connected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        Room = await _clientUtils.SyncRoom();
        StateHasChanged();
        if (Room!.Players.Any(x => !x.IsReadiedUp && Room.Owner.Id != x.Id))
        {
            _forceStartModalRef?.Show();
            return;
        }

        await ForceStartQuiz();
    }

    private async Task ForceStartQuiz()
    {
        if (Room!.Owner.Id == ClientState.Session!.Player.Id)
        {
            if (!IsStartingQuiz)
            {
                IsStartingQuiz = true;
                StateHasChanged();
                HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/StartQuiz",
                    new ReqStartQuiz(ClientState.Session.Token, Room.Id));
                if (res1.IsSuccessStatusCode)
                {
                }

                IsStartingQuiz = false;
            }
        }
    }

    private async Task OnReceiveQuizEntered()
    {
        // Double-entering QuizPage can trigger the Leave Quiz modal to pop up for some reason
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        if (_navigation.Uri.EndsWith("/QuizPage"))
        {
            return;
        }

        _locationChangingRegistration?.Dispose();
        _logger.LogInformation("Navigating from Room to Quiz");
        _navigation.NavigateTo("/QuizPage");
    }

    private async Task OnReceivePyramidEntered()
    {
        // Double-entering QuizPage can trigger the Leave Quiz modal to pop up for some reason
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        if (_navigation.Uri.EndsWith("/PyramidPage"))
        {
            return;
        }

        _locationChangingRegistration?.Dispose();
        _logger.LogInformation("Navigating from Room to Pyramid");
        _navigation.NavigateTo("/PyramidPage");
    }

    private async Task OnReceivePlayerJoinedRoom()
    {
        _logger.LogInformation("Syncing room because ReceivePlayerJoinedRoom");
        Room = await _clientUtils.SyncRoom();
        StateHasChanged();
        // _logger.LogInformation(JsonSerializer.Serialize(Room));
    }

    private async Task LeaveRoom()
    {
        // Room = await _clientUtils.SyncRoom();

        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerLeaving");
        // Room = await _clientUtils.SyncRoom();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        _locationChangingRegistration?.Dispose();
        _navigation.NavigateTo("/HotelPage");
    }

    private async Task CallStateHasChanged()
    {
        Room = await _clientUtils.SyncRoom();
        StateHasChanged();
    }

    private async Task OnReceiveUpdateRoomForRoom(Room room)
    {
        Room = room;
        if (_chatComponent != null)
        {
            _chatComponent.Chat = room.Chat;
            await _chatComponent.CallStateHasChanged();
        }

        StateHasChanged();
    }

    private async Task SendToggleReadiedUp()
    {
        await ClientState.Session!.hubConnection!.SendAsync("SendToggleReadiedUp");
    }

    private async Task OnclickChangeRoomNameAndPassword()
    {
        var res = await _client.GetAsync(
            $"Quiz/GetRoomPassword?token={ClientState.Session?.Token}&roomId={Room?.Id}");
        if (res.IsSuccessStatusCode)
        {
            RoomName = Room!.Name;
            RoomPassword = await res.Content.ReadAsStringAsync();
            _changeRoomNameAndPasswordModalRef?.Show();
        }
    }

    private async Task ChangeRoomNameAndPassword()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/ChangeRoomNameAndPassword",
            new ReqChangeRoomNameAndPassword(ClientState.Session!.Token, Room!.Id, RoomName!, RoomPassword!));
        if (res.IsSuccessStatusCode)
        {
            Room = await _clientUtils.SyncRoom();
            StateHasChanged();
            _changeRoomNameAndPasswordModalRef?.Hide();
        }
    }

    private async Task SendConvertSpectatorToPlayerInRoom()
    {
        await ClientState.Session!.hubConnection!.SendAsync("SendConvertSpectatorToPlayerInRoom");
    }

    private async Task SendConvertPlayerToSpectatorInRoom(int? playerId)
    {
        if (playerId != null)
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendConvertPlayerToSpectatorInRoom", playerId);
        }
    }

    private async Task OnclickTransferRoomOwnership(int playerId)
    {
        await ClientState.Session!.hubConnection!.SendAsync("SendTransferRoomOwnership", playerId);
    }

    private async Task OnclickKickFromRoom(int playerId)
    {
        await ClientState.Session!.hubConnection!.SendAsync("SendKickFromRoom", playerId);
    }

    private async Task OnReceiveKickedFromRoom()
    {
        await _jsRuntime.InvokeVoidAsync("alert", "You were kicked from the room.");
        _locationChangingRegistration?.Dispose();
        _navigation.NavigateTo("/HotelPage", true);
    }

    private async Task Onclick_InviteLink()
    {
        if (Room != null)
        {
            var res = await _client.GetAsync(
                $"Quiz/GetRoomPassword?token={ClientState.Session?.Token}&roomId={Room.Id}");
            if (res.IsSuccessStatusCode)
            {
                RoomPassword = await res.Content.ReadAsStringAsync();
            }

            string inviteLink = $"{_navigation.BaseUri}UseRoomInvitePage?roomId={Room.Id}";
            if (!string.IsNullOrEmpty(RoomPassword))
            {
                inviteLink += $"&password={RoomPassword}";
            }

            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", inviteLink);

            // This is kind of hacky. Do I care? Not really.
            InviteLinkButtonText = "Copied!";
            StateHasChanged();
            await Task.Delay(TimeSpan.FromSeconds(5));
            InviteLinkButtonText = "Invite link";
            StateHasChanged();
        }
    }

    private async Task OnClickButtonSongHistory()
    {
        if (Room?.Quiz != null && !ClientSongsHistory.Any())
        {
            HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/GetRoomSongHistory", Room.Id);
            if (res.IsSuccessStatusCode)
            {
                var serverSongHistory = await res.Content.ReadFromJsonAsync<Dictionary<int, SongHistory>>();
                if (serverSongHistory is not null)
                {
                    ClientSongsHistory = serverSongHistory;
                }
            }
        }

        await _songHistoryWrapperComponent!.Show();
    }

    private async Task Onclick_AddBotPlayer()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/AddBotPlayer", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }

    private async Task Onclick_ShuffleTeams()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/ShuffleTeams", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }

    private async Task Onclick_SortPlayersByTeamIds()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/SortPlayersByTeamIds", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }
}
