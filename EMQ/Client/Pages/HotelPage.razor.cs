﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

namespace EMQ.Client.Pages;

public partial class HotelPage
{
    private string Name { get; set; } = "";

    private string Password { get; set; } = "";

    // TODO allow user to change settings before creating room
    private QuizSettings QuizSettings { get; set; } = new() { };

    private List<Room> Rooms { get; set; } = new();

    public bool IsJoiningRoom = false;

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        IEnumerable<Room>? res = await Client.GetFromJsonAsync<IEnumerable<Room>>("Quiz/GetRooms");
        if (res is not null)
        {
            Rooms = res.ToList();
        }
    }

    private async Task CreateRoom()
    {
        if (ClientState.Session is null)
        {
            // todo warn not logged in
            return;
        }

        ReqCreateRoom req = new(ClientState.Session.Token, Name, Password, QuizSettings);
        HttpResponseMessage res = await Client.PostAsJsonAsync("Quiz/CreateRoom", req);
        int roomId = await res.Content.ReadFromJsonAsync<int>();

        await JoinRoom(roomId);
    }

    private async Task JoinRoom(int roomId)
    {
        // _logger.LogError(roomId.ToString());
        // _logger.LogError(Password);
        // _logger.LogError(JsonSerializer.Serialize(ClientState.Session));

        if (ClientState.Session is null)
        {
            // todo warn not logged in
            return;
        }

        IsJoiningRoom = true;
        StateHasChanged();

        HttpResponseMessage res1 = await Client.PostAsJsonAsync("Quiz/JoinRoom",
            new ReqJoinRoom(roomId, Password, ClientState.Session!.Player.Id));
        if (res1.IsSuccessStatusCode)
        {
            int waitTime = ((await res1.Content.ReadFromJsonAsync<ResJoinRoom>())!).WaitMs;
            if (waitTime > 0)
            {
                // todo display the wait time to the player
                Console.WriteLine($"waiting for {waitTime} ms to join room");
                await Task.Delay(waitTime);
            }

            ClientState.Session.RoomId = roomId;
            await _clientUtils.SaveSessionToLocalStorage();

            Navigation.NavigateTo("/RoomPage");
        }

        IsJoiningRoom = false;
        StateHasChanged();
    }
}