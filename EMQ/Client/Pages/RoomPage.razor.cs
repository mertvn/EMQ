using System;
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
        };
    }

    private QuizSettings ClientQuizSettings { get; set; } = new();

    private Room? Room { get; set; }

    private readonly Dictionary<string, (Type[] types, Func<object?[], Task> value)> _handlers;

    public bool ShowQuizSettings { get; set; }

    public bool IsStartingQuiz { get; set; }

    public SongSourceCategory[] AutocompleteCData { get; set; } = Array.Empty<SongSourceCategory>();

    private ChatComponent? _chatComponent;

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        await _clientConnectionManager.SetHandlers(_handlers);

        Room = await _clientUtils.SyncRoom();
        if (Room != null)
        {
            ClientQuizSettings =
                JsonSerializer.Deserialize<QuizSettings>(
                    JsonSerializer.Serialize(Room!.QuizSettings))!; // need a deep copy
            StateHasChanged();

            bool canHotjoin = Room.QuizSettings.IsHotjoinEnabled &&
                              Room!.Quiz?.QuizState.QuizStatus is QuizStatus.Playing;
            // breaks the leave button on QuizPage
            canHotjoin = false;
            if (canHotjoin)
            {
                Hotjoin();
            }

            AutocompleteCData =
                (await _client.GetFromJsonAsync<SongSourceCategory[]>("autocomplete/c.json", Utils.Jso))!;
            StateHasChanged();
        }
        else
        {
            // todo require reload etc.
        }
    }

    private async Task StartQuiz()
    {
        if (Room!.Owner.Id == ClientState.Session!.Player.Id)
        {
            if (!IsStartingQuiz)
            {
                IsStartingQuiz = true;
                HttpResponseMessage res1 = await Client.PostAsJsonAsync("Quiz/StartQuiz",
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
        _logger.LogInformation("Navigating from Room to Quiz");
        Navigation.NavigateTo("/QuizPage");
    }

    private async Task OnReceivePyramidEntered()
    {
        _logger.LogInformation("Navigating from Room to Pyramid");
        Navigation.NavigateTo("/PyramidPage");
    }

    private async Task OnReceivePlayerJoinedRoom()
    {
        _logger.LogInformation("Syncing room because ReceivePlayerJoinedRoom");
        Room = await _clientUtils.SyncRoom();
        StateHasChanged();
        // _logger.LogInformation(JsonSerializer.Serialize(Room));
    }

    private void Hotjoin()
    {
        _logger.LogInformation("Hotjoining Quiz");
        Navigation.NavigateTo("/QuizPage");
    }

    private void ResetQuizSettings()
    {
        ClientQuizSettings = new QuizSettings();
    }

    private async Task SendChangeRoomSettingsReq(QuizSettings clientQuizSettings)
    {
        if (Room!.Owner.Id == ClientState.Session!.Player.Id)
        {
            // todo room password
            HttpResponseMessage res1 = await Client.PostAsJsonAsync("Quiz/ChangeRoomSettings",
                new ReqChangeRoomSettings(
                    ClientState.Session.Token, Room.Id, "", clientQuizSettings));

            if (res1.IsSuccessStatusCode)
            {
                ShowQuizSettings = false;
                Room = await _clientUtils.SyncRoom();
                StateHasChanged();
            }
        }
    }

    private async Task OnclickShowQuizSettings()
    {
        Room = await _clientUtils.SyncRoom();
        if (Room?.QuizSettings != null)
        {
            ClientQuizSettings =
                JsonSerializer.Deserialize<QuizSettings>(
                    JsonSerializer.Serialize(Room!.QuizSettings))!; // need a deep copy
        }

        ShowQuizSettings = !ShowQuizSettings;
        StateHasChanged();
    }

    private async Task Onclick_Leave()
    {
        // Room = await _clientUtils.SyncRoom();

        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", "Really leave?");
        if (confirmed)
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendPlayerLeaving");
            // Room = await _clientUtils.SyncRoom();
            Navigation.NavigateTo("/HotelPage");
        }
    }

    private async Task RandomizeTags()
    {
        // todo
        if (!AutocompleteCData.Any())
        {
            return;
        }

        var rng = Random.Shared;

        var categoryFilters = new List<CategoryFilter>();
        for (int i = 1; i <= rng.Next(1, 6); i++)
        {
            var songSourceCategory = AutocompleteCData[rng.Next(AutocompleteCData.Length)];

            var trilean = (LabelKind)rng.Next(-1, 1); // we don't want Include here
            if (trilean is LabelKind.Exclude)
            {
                songSourceCategory.SpoilerLevel = SpoilerLevel.Major;
            }
            else
            {
                songSourceCategory.SpoilerLevel = SpoilerLevel.None;
            }

            var categoryFilter = new CategoryFilter(songSourceCategory, trilean);
            categoryFilters.Add(categoryFilter);
        }

        ClientQuizSettings.Filters.CategoryFilters = categoryFilters;
        await SendChangeRoomSettingsReq(ClientQuizSettings);
    }

    private async Task ClearTags()
    {
        ClientQuizSettings.Filters.CategoryFilters = new List<CategoryFilter>();
        await SendChangeRoomSettingsReq(ClientQuizSettings);
    }
}
