using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Client.Components;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using Microsoft.JSInterop;

namespace EMQ.Client.Pages;

public partial class HotelPage
{
    private QuizSettings QuizSettings { get; set; } = new() { };

    private List<Room> Rooms { get; set; } = new();

    public CreateNewRoomModel _createNewRoomModel { get; set; } = new();

    public bool IsJoiningRoom = false;

    private GenericModal? _passwordModalRef;

    public Guid SelectedRoomId { get; set; } = Guid.Empty;

    public string SelectedRoomPassword { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        IEnumerable<Room>? resInitial = await _client.GetFromJsonAsync<IEnumerable<Room>>("Auth/GetRooms");
        if (resInitial is not null)
        {
            Rooms = resInitial.ToList();
            StateHasChanged();
        }

        // todo important stop this if user navigates away
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync())
        {
            IEnumerable<Room>? res = await _client.GetFromJsonAsync<IEnumerable<Room>>("Auth/GetRooms");
            if (res is not null)
            {
                Rooms = res.ToList();
                StateHasChanged();
            }
        }
    }

    private async Task SendCreateRoomReq(CreateNewRoomModel createNewRoomModel)
    {
        if (ClientState.Session is null)
        {
            return;
        }

        ReqCreateRoom req = new(ClientState.Session.Token, createNewRoomModel.RoomName, createNewRoomModel.RoomPassword,
            QuizSettings);
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/CreateRoom", req);
        Guid roomId = await res.Content.ReadFromJsonAsync<Guid>();

        await JoinRoom(roomId, createNewRoomModel.RoomPassword);
    }

    private async Task JoinRoom(Guid roomId, string roomPassword)
    {
        // _logger.LogError(roomId.ToString());
        // _logger.LogError(Password);
        // _logger.LogError(JsonSerializer.Serialize(ClientState.Session));

        if (ClientState.Session is null)
        {
            return;
        }

        IsJoiningRoom = true;
        StateHasChanged();

        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/JoinRoom",
            new ReqJoinRoom(roomId, roomPassword, ClientState.Session.Token));
        if (res1.IsSuccessStatusCode)
        {
            await _clientUtils.SaveSessionToLocalStorage(); // todo why do we need to do this?

            var quizStatus = ((await res1.Content.ReadFromJsonAsync<ResJoinRoom>())!).QuizStatus;
            if (quizStatus == QuizStatus.Playing)
            {
                _navigation.NavigateTo("/QuizPage");
            }
            else
            {
                _navigation.NavigateTo("/RoomPage");
            }
        }
        else if (res1.StatusCode == HttpStatusCode.Unauthorized)
        {
            _passwordModalRef?.Show();
        }
        else
        {
            IEnumerable<Room>? res = await _client.GetFromJsonAsync<IEnumerable<Room>>("Auth/GetRooms");
            if (res is not null)
            {
                Rooms = res.ToList();
            }
        }

        IsJoiningRoom = false;
        StateHasChanged();
    }

    public class CreateNewRoomModel
    {
        [Required]
        [MaxLength(100)]
        public string RoomName { get; set; } = "Room";

        [MaxLength(16)]
        public string RoomPassword { get; set; } = "";
    }
}
