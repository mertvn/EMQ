using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace EMQ.Client.Components;

public partial class QuizSettingsComponent
{
    [Parameter]
    public Room? Room { get; set; }

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    [Parameter]
    public bool IsReadOnly { get; set; } = true;

    [Parameter]
    public bool IsQuizPage { get; set; }

    // we keep a separate copy of the quiz settings instead of using the one in Room
    // because we don't want the settings to get reset while someone is editing them
    private QuizSettings ClientQuizSettings { get; set; } = new();

    private SongSourceCategory? SelectedTag { get; set; }

    private AutocompleteA? SelectedArtist { get; set; }

    private AutocompleteCComponent AutocompleteCComponent { get; set; } = null!;

    private AutocompleteAComponent AutocompleteAComponent { get; set; } = null!;

    private Blazorise.Modal _modalRef = null!;

    private string _selectedTab = "TabGeneral";

    private EditContext EditContext = null!;

    protected override void OnInitialized()
    {
        SetNewEditContext(ClientQuizSettings);
    }

    protected override bool ShouldRender()
    {
        // no point rendering it on QuizPage where it'll be immutable
        // not using IsReadOnly here because that would cause non-owner players to miss out on setting changes
        return !IsQuizPage;
    }

    private void SetNewEditContext(QuizSettings quizSettings)
    {
        if (EditContext != null!)
        {
            EditContext.OnFieldChanged -= EditContext_OnFieldChanged;
        }

        EditContext = new EditContext(quizSettings);
        EditContext.OnFieldChanged += EditContext_OnFieldChanged;
    }

    private void EditContext_OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        // Console.WriteLine(e.FieldIdentifier.FieldName);
        // if (e.FieldIdentifier.FieldName is nameof(IntWrapper.Value) or nameof(ClientQuizSettings.NumSongs))
        // {
        //     RecalculateNumSongsAndSongTypeFilters();
        // }
    }

    private Task OnSelectedTabChanged(string name)
    {
        _selectedTab = name;
        return Task.CompletedTask;
    }

    private async Task SendChangeRoomSettingsReq(QuizSettings clientQuizSettings)
    {
        RecalculateNumSongsAndSongTypeFilters();

        bool isValid = EditContext.Validate();
        if (!isValid)
        {
            return;
        }

        if (Room!.Owner.Id == ClientState.Session!.Player.Id)
        {
            HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/ChangeRoomSettings",
                new ReqChangeRoomSettings(
                    ClientState.Session.Token, Room.Id, clientQuizSettings));

            if (res1.IsSuccessStatusCode)
            {
                await _modalRef.Hide();
                Room = await _clientUtils.SyncRoom();
                StateHasChanged();
                ParentStateHasChangedCallback?.Invoke();
            }
        }
    }

    public async Task OnclickShowQuizSettings()
    {
        Room = await _clientUtils.SyncRoom();
        if (Room?.QuizSettings != null)
        {
            ClientQuizSettings =
                JsonSerializer.Deserialize<QuizSettings>(
                    JsonSerializer.Serialize(Room!.QuizSettings))!; // need a deep copy
            SetNewEditContext(ClientQuizSettings);
        }

        await _modalRef.Show();
        StateHasChanged();
        ParentStateHasChangedCallback?.Invoke();
    }

    private void ResetQuizSettings()
    {
        Console.WriteLine("ResetQuizSettings");
        ClientQuizSettings = new QuizSettings();
        SetNewEditContext(ClientQuizSettings);
    }

    private async Task RandomizeTags()
    {
        if (!AutocompleteCComponent.AutocompleteData.Any())
        {
            return;
        }

        var rng = Random.Shared;

        var categoryFilters = new List<CategoryFilter>();
        for (int i = 1; i <= rng.Next(1, 6); i++)
        {
            var songSourceCategory =
                AutocompleteCComponent.AutocompleteData[rng.Next(AutocompleteCComponent.AutocompleteData.Length)];

            LabelKind trilean;
            if (i == 1)
            {
                // we want at least 1 Maybe
                trilean = LabelKind.Maybe;
            }
            else
            {
                // we don't want Include here
                trilean = (LabelKind)rng.Next(-1, 1);
            }

            if (trilean is LabelKind.Exclude)
            {
                songSourceCategory.SpoilerLevel = SpoilerLevel.Major;
            }
            else
            {
                songSourceCategory.SpoilerLevel = SpoilerLevel.None;
            }

            songSourceCategory.Rating = 1;
            var categoryFilter = new CategoryFilter(songSourceCategory, trilean);
            categoryFilters.Add(categoryFilter);
        }

        ClientQuizSettings.Filters.CategoryFilters = categoryFilters;
    }

    private async Task RandomizeArtists()
    {
        if (!AutocompleteAComponent.AutocompleteData.Any())
        {
            return;
        }

        var rng = Random.Shared;

        var categoryFilters = new List<ArtistFilter>();
        for (int i = 1; i <= rng.Next(1, 6); i++)
        {
            var songSourceCategory =
                AutocompleteAComponent.AutocompleteData[rng.Next(AutocompleteAComponent.AutocompleteData.Length)];

            LabelKind trilean;
            if (i == 1)
            {
                // we want at least 1 Maybe
                trilean = LabelKind.Maybe;
            }
            else
            {
                // we don't want Include here
                trilean = (LabelKind)rng.Next(-1, 1);
            }

            var categoryFilter = new ArtistFilter(songSourceCategory, trilean);
            categoryFilters.Add(categoryFilter);
        }

        ClientQuizSettings.Filters.ArtistFilters = categoryFilters;
    }

    private async Task ClearTags()
    {
        ClientQuizSettings.Filters.CategoryFilters = new List<CategoryFilter>();
    }

    private async Task ClearArtists()
    {
        ClientQuizSettings.Filters.ArtistFilters = new List<ArtistFilter>();
    }

    private async Task SelectedResultChangedC()
    {
        // Console.WriteLine("st:" + JsonSerializer.Serialize(selectedTag));
        if (SelectedTag != null)
        {
            SelectedTag.SpoilerLevel = SpoilerLevel.None;
            SelectedTag.Rating = 1f;

            ClientQuizSettings.Filters.CategoryFilters.Add(new CategoryFilter(SelectedTag, LabelKind.Maybe));
            // Console.WriteLine("cf:" + JsonSerializer.Serialize(ClientQuizSettings.Filters.CategoryFilters));

            // need to call it twice for it to work
            await AutocompleteCComponent.ClearInputField();
            await AutocompleteCComponent.ClearInputField();
        }
    }

    private async Task SelectedResultChangedA()
    {
        // Console.WriteLine("st:" + JsonSerializer.Serialize(selectedTag));
        if (SelectedArtist != null)
        {
            ClientQuizSettings.Filters.ArtistFilters.Add(new ArtistFilter(SelectedArtist, LabelKind.Maybe));
            // Console.WriteLine("cf:" + JsonSerializer.Serialize(ClientQuizSettings.Filters.CategoryFilters));

            // need to call it twice for it to work
            await AutocompleteAComponent.ClearInputField();
            await AutocompleteAComponent.ClearInputField();
        }
    }

    private async Task RemoveTag(int tagId)
    {
        ClientQuizSettings.Filters.CategoryFilters.RemoveAll(x => x.SongSourceCategory.Id == tagId);
    }

    private async Task RemoveArtist(int artistId)
    {
        ClientQuizSettings.Filters.ArtistFilters.RemoveAll(x => x.Artist.AId == artistId);
    }

    private void RecalculateNumSongsAndSongTypeFilters()
    {
        ClientQuizSettings.NumSongs = Math.Clamp(ClientQuizSettings.NumSongs, 1, 100);
        foreach ((SongSourceSongType key, IntWrapper? value) in ClientQuizSettings.Filters.SongSourceSongTypeFilters)
        {
            ClientQuizSettings.Filters.SongSourceSongTypeFilters[key] = new IntWrapper(Math.Clamp(value.Value, 0, 100));
        }

        // doing this breaks modifying by the InputNumber
        // while (ClientQuizSettings.SongSourceSongTypeFiltersSum < ClientQuizSettings.NumSongs)
        // {
        //     (SongSourceSongType key, IntWrapper? value) =
        //         ClientQuizSettings.Filters.SongSourceSongTypeFilters.Last(x => x.Value.Value < 100);
        //
        //     ClientQuizSettings.Filters.SongSourceSongTypeFilters[key] = new IntWrapper(Math.Max(0,
        //         value.Value + (ClientQuizSettings.NumSongs - ClientQuizSettings.SongSourceSongTypeFiltersSum)));
        // }

        while (ClientQuizSettings.SongSourceSongTypeFiltersSum > ClientQuizSettings.NumSongs)
        {
            (SongSourceSongType key, IntWrapper? value) =
                ClientQuizSettings.Filters.SongSourceSongTypeFilters.Last(x => x.Value.Value > 0);

            ClientQuizSettings.Filters.SongSourceSongTypeFilters[key] = new IntWrapper(Math.Max(0,
                value.Value - (ClientQuizSettings.SongSourceSongTypeFiltersSum - ClientQuizSettings.NumSongs)));
        }

        // StateHasChanged();
    }
}
