using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Client.Components;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using Microsoft.JSInterop;

namespace EMQ.Client.Pages;

public partial class HotelPage
{
    // TODO allow user to change settings before creating room
    private QuizSettings QuizSettings { get; set; } = new() { };

    private List<Room> Rooms { get; set; } = new();

    public CreateNewRoomModel _createNewRoomModel { get; set; } = new();

    public bool IsJoiningRoom = false;

    private GenericModal? _passwordModalRef;

    public int SelectedRoomId { get; set; } = -1;

    public string SelectedRoomPassword { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        IEnumerable<Room>? res = await _client.GetFromJsonAsync<IEnumerable<Room>>("Quiz/GetRooms");
        if (res is not null)
        {
            Rooms = res.ToList();
        }
    }

    private async Task SendCreateRoomReq(CreateNewRoomModel createNewRoomModel)
    {
        if (ClientState.Session is null)
        {
            // todo warn not logged in
            return;
        }

        ReqCreateRoom req = new(ClientState.Session.Token, createNewRoomModel.RoomName, createNewRoomModel.RoomPassword,
            QuizSettings);
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/CreateRoom", req);
        int roomId = await res.Content.ReadFromJsonAsync<int>();

        await JoinRoom(roomId, createNewRoomModel.RoomPassword);
    }

    private async Task JoinRoom(int roomId, string roomPassword)
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

        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/JoinRoom",
            new ReqJoinRoom(roomId, roomPassword, ClientState.Session.Player.Id));
        if (res1.IsSuccessStatusCode)
        {
            await _clientUtils.SaveSessionToLocalStorage();

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
