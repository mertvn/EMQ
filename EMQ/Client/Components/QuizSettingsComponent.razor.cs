using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

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

    [Parameter]
    public bool IsLibraryPage { get; set; }

    // we keep a separate copy of the quiz settings instead of using the one in Room
    // because we don't want the settings to get reset while someone is editing them
    public QuizSettings ClientQuizSettings { get; set; } = new();

    private SongSourceCategory? SelectedTag { get; set; }

    private AutocompleteA? SelectedArtist { get; set; }

    private AutocompleteMst? SelectedSource { get; set; }

    private AutocompleteCollection? SelectedCollection { get; set; }

    private AutocompleteCComponent AutocompleteCComponent { get; set; } = null!;

    private AutocompleteAComponent AutocompleteAComponent { get; set; } = null!;

    private GuessInputComponent GuessInputComponent { get; set; } = null!;

    private AutocompleteCollectionComponent AutocompleteCollectionComponent { get; set; } = null!;

    private Blazorise.Modal _modalRef = null!;

    private string _selectedTab { get; set; } = "TabGeneral";

    private EditContext EditContext = null!;

    private ValidationMessageStore ValidationMessageStore = null!;

    private GenericModal _loadPresetModalRef = null!;

    private GenericModal _savePresetModalRef = null!;

    public string NewPresetName { get; set; } = "";

    public List<ResGetUserQuizSettings> Presets { get; set; } = new();

    public string SelectedPresetName { get; set; } = "";

    public string SharePresetButtonText { get; set; } = "Share";

    public string LoadFromCodeB64 { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        SetNewEditContext(ClientQuizSettings);

        // todo? move to load preset opening
        while (ClientState.Session is null)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        if (AuthStuff.HasPermission(ClientState.Session, PermissionKind.StoreQuizSettings))
        {
            var res = await SendGetUserQuizSettingsReq(ClientState.Session.Token);
            if (res != null)
            {
                Presets = res;
            }
        }
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
        ValidationMessageStore = new ValidationMessageStore(EditContext);
    }

    private void EditContext_OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        // Console.WriteLine(e.FieldIdentifier.FieldName);
        // if (e.FieldIdentifier.FieldName is nameof(IntWrapper.Value) or nameof(ClientQuizSettings.NumSongs))
        // {
        //     RecalculateNumSongsAndSongTypeFilters();
        // }
    }

    private async Task SendChangeRoomSettingsReq(QuizSettings clientQuizSettings)
    {
        RecalculateNumSongsAndSongTypeFilters();
        RecalculateListReadKindFilters();

        ValidationMessageStore.Clear();
        if (ClientQuizSettings.SongSelectionKind == SongSelectionKind.Looting)
        {
            if (!(ClientQuizSettings.Filters.ListReadKindFiltersIsAllRandom ||
                  ClientQuizSettings.Filters.ListReadKindFiltersIsOnlyRead))
            {
                ValidationMessageStore.Add(() => ClientQuizSettings.Filters.ListReadKindFilters,
                    "Looting mode requires either only Read or only Random list status settings.");
            }

            EditContext.NotifyValidationStateChanged();
        }

        if (ClientQuizSettings.AnsweringKind is AnsweringKind.MultipleChoice or AnsweringKind.Mixed &&
            !((ClientQuizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Random, out bool r) && r) ||
              (ClientQuizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Lists, out bool l) && l)))
        {
            ValidationMessageStore.Add(() => ClientQuizSettings.Filters.ListReadKindFilters,
                "At least one of the following types must be enabled for Multiple Choice: " +
                $"\"{MCOptionKind.Random.GetDescription()}\", or \"{MCOptionKind.Lists.GetDescription()}\".");

            EditContext.NotifyValidationStateChanged();
        }

        bool isValid = EditContext.Validate();
        if (!isValid)
        {
            return;
        }

        if (IsLibraryPage)
        {
            await _modalRef.Hide();
            ParentStateHasChangedCallback?.Invoke();
            return;
        }

        if (Room!.Owner.Id == ClientState.Session!.Player.Id)
        {
            HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/ChangeRoomSettings",
                new ReqChangeRoomSettings(
                    ClientState.Session.Token, Room.Id, clientQuizSettings));

            if (res.IsSuccessStatusCode)
            {
                await _modalRef.Hide();
                Room = await _clientUtils.SyncRoom();
                StateHasChanged();
                ParentStateHasChangedCallback?.Invoke();
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("alert",
                    $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
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
                    JsonSerializer.Serialize(Room!.QuizSettings))!; // need a deep copy  // todo use Clone()
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

        var categoryFilters = new List<CategoryFilter>();
        for (int i = 1; i <= Random.Shared.Next(1, 6); i++)
        {
            var songSourceCategory =
                AutocompleteCComponent.AutocompleteData[
                    Random.Shared.Next(AutocompleteCComponent.AutocompleteData.Length)];

            LabelKind trilean;
            if (i == 1)
            {
                // we want at least 1 Maybe
                trilean = LabelKind.Maybe;
            }
            else
            {
                // we don't want Include here
                trilean = (LabelKind)Random.Shared.Next(-1, 1);
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

        var artistFilters = new List<ArtistFilter>();
        for (int i = 1; i <= Random.Shared.Next(1, 6); i++)
        {
            var artist =
                AutocompleteAComponent.AutocompleteData[
                    Random.Shared.Next(AutocompleteAComponent.AutocompleteData.Length)];

            LabelKind trilean;
            if (i == 1)
            {
                // we want at least 1 Maybe
                trilean = LabelKind.Maybe;
            }
            else
            {
                // we don't want Include here
                trilean = (LabelKind)Random.Shared.Next(-1, 1);
            }

            var artistFilter = new ArtistFilter(artist, trilean, SongArtistRole.Vocals, false);
            artistFilters.Add(artistFilter);
        }

        ClientQuizSettings.Filters.ArtistFilters = artistFilters;
    }

    private async Task RandomizeSources()
    {
        if (!GuessInputComponent.AutocompleteData.Any())
        {
            return;
        }

        var sourceFilters = new List<SongSourceFilter>();
        for (int i = 1; i <= Random.Shared.Next(1, 51); i++)
        {
            var source =
                GuessInputComponent.AutocompleteData[
                    Random.Shared.Next(GuessInputComponent.AutocompleteData.Length)];

            LabelKind trilean;
            if (i == 1)
            {
                // we want at least 1 Maybe
                trilean = LabelKind.Maybe;
            }
            else
            {
                // we don't want Include here
                trilean = (LabelKind)Random.Shared.Next(-1, 1);
            }

            var filter = new SongSourceFilter(source, trilean);
            sourceFilters.Add(filter);
        }

        ClientQuizSettings.Filters.SongSourceFilters = sourceFilters;
    }

    private async Task RandomizeCollections()
    {
        if (!AutocompleteCollectionComponent.AutocompleteData.Any())
        {
            return;
        }

        var collectionFilters = new List<CollectionFilter>();
        for (int i = 1; i <= Random.Shared.Next(1, 6); i++)
        {
            var collection =
                AutocompleteCollectionComponent.AutocompleteData[
                    Random.Shared.Next(AutocompleteCollectionComponent.AutocompleteData.Length)];

            LabelKind trilean;
            if (i == 1)
            {
                // we want at least 1 Maybe
                trilean = LabelKind.Maybe;
            }
            else
            {
                // we don't want Include here
                trilean = (LabelKind)Random.Shared.Next(-1, 1);
            }

            var filter = new CollectionFilter(collection, trilean);
            collectionFilters.Add(filter);
        }

        ClientQuizSettings.Filters.CollectionFilters = collectionFilters;
    }

    private async Task ClearTags()
    {
        ClientQuizSettings.Filters.CategoryFilters = new List<CategoryFilter>();
    }

    private async Task ClearArtists()
    {
        ClientQuizSettings.Filters.ArtistFilters = new List<ArtistFilter>();
    }

    private async Task ClearSources()
    {
        ClientQuizSettings.Filters.SongSourceFilters = new List<SongSourceFilter>();
    }

    private async Task ClearCollections()
    {
        ClientQuizSettings.Filters.CollectionFilters = new List<CollectionFilter>();
    }

    private async Task SelectedResultChangedC()
    {
        if (SelectedTag != null)
        {
            SelectedTag.SpoilerLevel = SpoilerLevel.None;
            SelectedTag.Rating = 1f;
            ClientQuizSettings.Filters.CategoryFilters.Add(new CategoryFilter(SelectedTag, LabelKind.Maybe));

            // need to call it twice for it to work // todo test if this is still the case
            await AutocompleteCComponent.ClearInputField();
            await AutocompleteCComponent.ClearInputField();
        }
    }

    private async Task SelectedResultChangedA()
    {
        if (SelectedArtist != null)
        {
            ClientQuizSettings.Filters.ArtistFilters.Add(new ArtistFilter(SelectedArtist, LabelKind.Maybe,
                SongArtistRole.Vocals, false));

            await AutocompleteAComponent.ClearInputField();
            await AutocompleteAComponent.ClearInputField();
        }
    }

    private async Task SelectedResultChangedMst()
    {
        if (SelectedSource != null)
        {
            ClientQuizSettings.Filters.SongSourceFilters.Add(new SongSourceFilter(SelectedSource, LabelKind.Maybe));

            await GuessInputComponent.ClearInputField();
            await GuessInputComponent.ClearInputField();
        }
    }

    private async Task SelectedResultChangedCollection()
    {
        if (SelectedCollection != null)
        {
            ClientQuizSettings.Filters.CollectionFilters.Add(new CollectionFilter(SelectedCollection, LabelKind.Maybe));

            await AutocompleteCollectionComponent.ClearInputField();
            await AutocompleteCollectionComponent.ClearInputField();
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

    private async Task RemoveSource(int sourceId)
    {
        ClientQuizSettings.Filters.SongSourceFilters.RemoveAll(x => x.AutocompleteMst.MSId == sourceId);
    }

    private async Task RemoveCollection(int collectionId)
    {
        ClientQuizSettings.Filters.CollectionFilters.RemoveAll(x => x.AutocompleteCollection.CoId == collectionId);
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

    private void RecalculateListReadKindFilters()
    {
        foreach ((ListReadKind key, IntWrapper? value) in ClientQuizSettings.Filters.ListReadKindFilters)
        {
            ClientQuizSettings.Filters.ListReadKindFilters[key] = new IntWrapper(Math.Clamp(value.Value, 0, 100));
        }

        while (ClientQuizSettings.ListReadKindFiltersSum > ClientQuizSettings.NumSongs)
        {
            (ListReadKind key, IntWrapper? value) =
                ClientQuizSettings.Filters.ListReadKindFilters.Last(x => x.Value.Value > 0);

            ClientQuizSettings.Filters.ListReadKindFilters[key] = new IntWrapper(Math.Max(0,
                value.Value - (ClientQuizSettings.ListReadKindFiltersSum - ClientQuizSettings.NumSongs)));
        }

        // StateHasChanged();
    }

    private async Task SendStoreUserQuizSettingsReq(string name)
    {
        if (ClientState.Session == null)
        {
            return;
        }

        string b64 = ClientQuizSettings.SerializeToBase64String_PB();
        Console.WriteLine(b64);
        Console.WriteLine(b64.Length);

        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Auth/StoreUserQuizSettings",
            new ReqStoreUserQuizSettings(ClientState.Session.Token, name, b64));

        if (res1.IsSuccessStatusCode)
        {
            var resGetUserQuizSettings = await SendGetUserQuizSettingsReq(ClientState.Session.Token);
            if (resGetUserQuizSettings != null)
            {
                Presets = resGetUserQuizSettings;
                NewPresetName = "";
                _savePresetModalRef.Hide();
            }
            else
            {
                // todo warn error
            }
        }
        else
        {
            // todo warn error
        }

        StateHasChanged();
        ParentStateHasChangedCallback?.Invoke();
    }

    private async Task<List<ResGetUserQuizSettings>?> SendGetUserQuizSettingsReq(string token)
    {
        var resGet = await _client.GetAsync($"Auth/GetUserQuizSettings?token={token}");
        if (resGet.IsSuccessStatusCode)
        {
            List<ResGetUserQuizSettings> resGetUserQuizSettings =
                (await resGet.Content.ReadFromJsonAsync<List<ResGetUserQuizSettings>>())!;
            return resGetUserQuizSettings;
        }
        else
        {
            return null;
        }
    }

    private async Task ApplyUserQuizSettings(string name)
    {
        try
        {
            var preset = Presets.SingleOrDefault(x => x.Name == name);
            if (preset == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(preset.B64))
            {
                ClientQuizSettings = preset.B64.DeserializeFromBase64String_PB<QuizSettings>();
                LoadFromCodeB64 = "";
                _loadPresetModalRef.Hide();
                SetNewEditContext(ClientQuizSettings);
                StateHasChanged();
                ParentStateHasChangedCallback?.Invoke();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // todo warn user
        }
    }

    private async Task ApplyUserQuizSettingsFromB64(string b64)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(b64))
            {
                ClientQuizSettings = b64.DeserializeFromBase64String_PB<QuizSettings>();
                LoadFromCodeB64 = "";
                _loadPresetModalRef.Hide();
                SetNewEditContext(ClientQuizSettings);
                StateHasChanged();
                ParentStateHasChangedCallback?.Invoke();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // todo warn user
        }
    }

    private async Task Onclick_SharePreset(string name)
    {
        var preset = Presets.SingleOrDefault(x => x.Name == name);
        if (preset == null)
        {
            return;
        }

        await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", preset.B64);

        SharePresetButtonText = $"Copied!";
        StateHasChanged();
        await Task.Delay(TimeSpan.FromSeconds(2));
        SharePresetButtonText = "Share";
        StateHasChanged();
    }

    private async Task Onclick_DeletePreset(string name)
    {
        if (ClientState.Session == null)
        {
            return;
        }

        var preset = Presets.SingleOrDefault(x => x.Name == name);
        if (preset == null)
        {
            return;
        }

        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Auth/DeleteUserQuizSettings",
            new ReqDeleteUserQuizSettings(ClientState.Session.Token, preset.Name));

        if (res1.IsSuccessStatusCode)
        {
            var resGetUserQuizSettings = await SendGetUserQuizSettingsReq(ClientState.Session.Token);
            if (resGetUserQuizSettings != null)
            {
                Presets = resGetUserQuizSettings;
            }
            else
            {
                // todo warn error
            }
        }
        else
        {
            // todo warn user
        }

        StateHasChanged();
        ParentStateHasChangedCallback?.Invoke();
    }
}
