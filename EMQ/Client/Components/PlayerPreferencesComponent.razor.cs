using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Juliet.Model.Param;
using Juliet.Model.VNDBObject;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class PlayerPreferencesComponent
{
    private Blazorise.Modal _modalRef = null!;

    private string _selectedTab { get; set; } = "TabGeneral";

    private string _selectedTabLists { get; set; } = UserListDatabaseKind.VNDB.ToString();

    public bool InProgress { get; set; }

    public List<string> LoginProgressDisplay { get; set; } = new();

    private string? ClientVndbApiToken { get; set; }

    public List<UserLabelPreset> Presets { get; set; } = new();

    public string SelectedPresetName { get; set; } = "";

    public SongSourceSongTypeMode SelectedSSSTM { get; set; } = SongSourceSongTypeMode.Vocals;

    public DonorBenefitKind SelectedDonorBenefitKind { get; set; }

    // 1 more char. than the longest preset name allowed
    public const string CreateNewPresetValue = "-----------------------------------------------------------------";

    public Avatar? ClientAvatar { get; set; }

    public DonorBenefit? ClientDonorBenefit { get; set; }

    public Dictionary<string, LabelStats?> LabelStats { get; set; } = new();

    private GuessInputComponent _guessInputComponentRef = null!;

    private string? selectedMusicSourceTitle { get; set; }

    protected override async Task OnInitializedAsync()
    {
        while (ClientState.Session is null)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        InProgress = true;
        var resGet = await _client.GetAsync("Auth/GetUserLabelPresets");
        if (resGet.IsSuccessStatusCode)
        {
            Presets = (await resGet.Content.ReadFromJsonAsync<List<UserLabelPreset>>())!;
            SelectedPresetName = ClientState.Session.ActiveUserLabelPresetName ?? "";
        }

        ClientAvatar = ClientState.Session.Player.Avatar;
        ClientDonorBenefit = ClientState.Session.Player.DonorBenefit;
        await FetchMissingSongSourcesForEMQTab();

        InProgress = false;
    }

    private async Task UpdatePlayerPreferences(PlayerPreferences playerPreferencesModel)
    {
        // HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/UpdatePlayerPreferences",
        //     new ReqUpdatePlayerPreferences(ClientState.Session!.Token, playerPreferencesModel));
        //
        // if (res.IsSuccessStatusCode)
        // {
        //     ClientState.Session.Player.Preferences = (await res.Content.ReadFromJsonAsync<PlayerPreferences>())!;
        //     await _clientUtils.SaveSessionToLocalStorage();
        //     await _modalRef.Hide();
        // }
        // else
        // {
        // }

        ClientState.Preferences = playerPreferencesModel;
        await _clientUtils.SavePreferencesToLocalStorage();
        await _modalRef.Hide();

        StateHasChanged();
    }

    public void OnclickButtonPreferences()
    {
        StateHasChanged();
        _modalRef.Show();
    }

    public async Task<List<Label>> FetchLabelsInner(PlayerVndbInfo vndbInfo)
    {
        List<Label> newLabels = new();
        switch (vndbInfo.DatabaseKind)
        {
            case UserListDatabaseKind.VNDB:
                VNDBLabel[] vndbLabels = await VndbMethods.GetLabels(vndbInfo);
                foreach (VNDBLabel vndbLabel in vndbLabels)
                {
                    newLabels.Add(Label.FromVndbLabel(vndbLabel));
                }

                break;
            case UserListDatabaseKind.MAL:
                newLabels.Add(new Label { Id = 1, IsPrivate = false, Name = "Watching" });
                newLabels.Add(new Label { Id = 2, IsPrivate = false, Name = "Completed" });
                newLabels.Add(new Label { Id = 3, IsPrivate = false, Name = "On-Hold" });
                newLabels.Add(new Label { Id = 4, IsPrivate = false, Name = "Dropped" });
                newLabels.Add(new Label { Id = 6, IsPrivate = false, Name = "Plan to Watch" });
                break;
        }

        Console.WriteLine(vndbInfo.Labels!.Count + "-" + newLabels.Count);
        List<Label> final = Label.MergeLabels(vndbInfo.Labels!, newLabels);
        return final;
    }

    private async Task OnLabelKindChanged(Label label, LabelKind newLabelKind, PlayerVndbInfo vndbInfo)
    {
        if (label.Kind == newLabelKind)
        {
            return;
        }

        InProgress = true;
        label = await UpdateLabel(label, newLabelKind, vndbInfo);

        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/UpdateLabel",
            new ReqUpdateLabel(ClientState.Session!.Token, label, vndbInfo.DatabaseKind));

        if (res.IsSuccessStatusCode)
        {
            var updatedLabel = await res.Content.ReadFromJsonAsync<Label>();
            if (updatedLabel != null)
            {
                Label oldLabel = ClientState.VndbInfo.First(x => x.DatabaseKind == vndbInfo.DatabaseKind).Labels!
                    .Single(x => x.Id == updatedLabel.Id);
                oldLabel.Kind = updatedLabel.Kind;
                oldLabel.VNs = updatedLabel.VNs;
            }
        }
        else
        {
            // todo warn user & restore old label state
            InProgress = false;
            throw new Exception();
        }

        InProgress = false;
    }

    private async Task RefreshLabel(Label label, LabelKind newLabelKind, PlayerVndbInfo vndbInfo)
    {
        await OnLabelKindChanged(label, LabelKind.Maybe, vndbInfo);
        await OnLabelKindChanged(label, newLabelKind, vndbInfo);
    }

    public async Task<Label> UpdateLabel(Label label, LabelKind newLabelKind, PlayerVndbInfo vndbInfo)
    {
        if (ClientState.Session != null && vndbInfo.Labels != null)
        {
            Console.WriteLine($"{vndbInfo.VndbId}: {label.Id} ({label.Name}), {label.Kind} => {newLabelKind}");
            label.Kind = newLabelKind;

            var newVns = new Dictionary<string, int>();
            switch (label.Kind)
            {
                case LabelKind.Maybe:
                    break;
                case LabelKind.Include:
                case LabelKind.Exclude:
                    var grabbed = new List<Label>();
                    switch (vndbInfo.DatabaseKind)
                    {
                        case UserListDatabaseKind.VNDB:
                            grabbed = await VndbMethods.GrabPlayerVNsFromVndb(new PlayerVndbInfo()
                            {
                                VndbId = vndbInfo.VndbId,
                                VndbApiToken = ClientVndbApiToken,
                                Labels = new List<Label>() { label },
                                DatabaseKind = vndbInfo.DatabaseKind,
                            });
                            break;
                        case UserListDatabaseKind.MAL:
                            grabbed = await MalMethods.ProxyGrabPlayerAnimeFromMal(_client,
                                new PlayerVndbInfo()
                                {
                                    VndbId = vndbInfo.VndbId,
                                    VndbApiToken = ClientVndbApiToken,
                                    Labels = new List<Label>() { label },
                                    DatabaseKind = vndbInfo.DatabaseKind,
                                });
                            break;
                    }

                    newVns = grabbed.SingleOrDefault()?.VNs ?? new Dictionary<string, int>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (vndbInfo.DatabaseKind != UserListDatabaseKind.EMQ)
            {
                label.VNs = newVns;
            }

            return label;
        }
        else
        {
            // should never be hit under normal circumstances
            throw new Exception();
        }
    }

    public async Task SetVndbInfo(PlayerVndbInfo vndbInfo)
    {
        InProgress = true;
        LoginProgressDisplay.Clear();
        StateHasChanged();

        if (vndbInfo.DatabaseKind == UserListDatabaseKind.VNDB)
        {
            if (!string.IsNullOrWhiteSpace(ClientVndbApiToken))
            {
                LoginProgressDisplay.Add("Validating VNDB API Token...");
                var resAuth = await Juliet.Api.GET_authinfo(new Param() { APIToken = ClientVndbApiToken });
                if (resAuth != null)
                {
                    const string vndbPermName = "listread"; // todo
                    if (!resAuth.Permissions.Contains(vndbPermName))
                    {
                        vndbInfo.VndbId = "";
                        LoginProgressDisplay.Add(
                            $"Error: VNDB API Token does not have the necessary permissions: {vndbPermName}");
                        StateHasChanged();
                    }
                    else
                    {
                        vndbInfo.VndbId = resAuth.Id;
                        LoginProgressDisplay.Add("Successfully validated VNDB API Token.");
                        StateHasChanged();
                    }
                }
                else
                {
                    vndbInfo.VndbId = "";
                    LoginProgressDisplay.Add("Error: Failed to validate VNDB API Token.");
                    StateHasChanged();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            vndbInfo.VndbId = "";
            if (vndbInfo.DatabaseKind == UserListDatabaseKind.VNDB)
            {
                ClientVndbApiToken = "";
            }

            vndbInfo.Labels = new List<Label>();
        }
        else
        {
            vndbInfo.Labels = new List<Label>();
            if (vndbInfo.DatabaseKind == UserListDatabaseKind.VNDB)
            {
                vndbInfo.VndbApiToken = ClientVndbApiToken;
            }

            vndbInfo.Labels = await FetchLabelsInner(vndbInfo);

            // we try processing playing, finished, stalled, voted, EMQ-wl, and EMQ-bl labels by default
            foreach (Label label in vndbInfo.Labels)
            {
                switch (label.Name.ToLowerInvariant())
                {
                    case "playing":
                    case "finished":
                    case "stalled":
                    case "voted":
                    case "emq-wl":
                        label.Kind = LabelKind.Include;
                        break;
                    case "emq-bl":
                        label.Kind = LabelKind.Exclude;
                        break;
                }
            }

            List<Label> vns;
            switch (vndbInfo.DatabaseKind)
            {
                case UserListDatabaseKind.VNDB:
                    vns = await VndbMethods.GrabPlayerVNsFromVndb(vndbInfo);
                    break;
                case UserListDatabaseKind.MAL:
                    vns = await MalMethods.ProxyGrabPlayerAnimeFromMal(_client, vndbInfo);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var intersect = Label.MergeLabels(vndbInfo.Labels, vns);
            foreach (Label label in vns)
            {
                if (!intersect.Contains(label))
                {
                    vndbInfo.Labels.Add(label);
                }
            }
        }

        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/SetVndbInfo",
            new ReqSetVndbInfo(ClientState.Session!.Token, ClientState.VndbInfo));

        if (res.IsSuccessStatusCode)
        {
            ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<List<PlayerVndbInfo>>())!;
        }
        else
        {
            InProgress = false;
            throw new Exception();
        }

        InProgress = false;
        StateHasChanged();
    }

    private async Task OnSelectedPresetChanged(string value)
    {
        InProgress = true;
        if (value == CreateNewPresetValue)
        {
            string? promptResult =
                (await _jsRuntime.InvokeAsync<string?>("prompt", "Enter new preset name (64 chars max.)"))?.Trim();
            if (!string.IsNullOrWhiteSpace(promptResult) && promptResult.Length is > 0 and <= 64)
            {
                if (!Presets.Any(x => string.Equals(x.name, promptResult, StringComparison.OrdinalIgnoreCase)))
                {
                    var res = await _client.PostAsJsonAsync("Auth/UpsertUserLabelPreset", promptResult);
                    if (res.IsSuccessStatusCode)
                    {
                        Presets.Add(new UserLabelPreset { name = promptResult });
                        SelectedPresetName = promptResult;
                        ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<List<PlayerVndbInfo>>())!;
                        ClientState.VndbInfo.AddRange(Enum.GetValues<UserListDatabaseKind>()
                            .Where(x => x == UserListDatabaseKind.VNDB)
                            .Select(x => new PlayerVndbInfo() { DatabaseKind = x }));
                    }
                    else
                    {
                        SelectedPresetName = "";
                        InProgress = false;
                        throw new Exception();
                    }
                }
            }
        }
        else
        {
            var res = await _client.PostAsJsonAsync("Auth/UpsertUserLabelPreset", value);
            if (res.IsSuccessStatusCode)
            {
                SelectedPresetName = value;
                ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<List<PlayerVndbInfo>>())!;
            }
            else
            {
                SelectedPresetName = "";
                InProgress = false;
                throw new Exception();
            }
        }

        InProgress = false;
    }

    private async Task Onclick_DeletePreset(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete {name}?");
        if (!confirmed)
        {
            return;
        }

        var preset = Presets.SingleOrDefault(x => x.name == name);
        if (preset == null)
        {
            return;
        }

        InProgress = true;
        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/DeleteUserLabelPreset", preset.name);
        if (res.IsSuccessStatusCode)
        {
            SelectedPresetName = "";
            Presets.Remove(preset);
            ClientState.VndbInfo = new();
        }
        else
        {
            // todo warn user
        }

        InProgress = false;
    }

    private async Task OnSelectedCharacterChanged(AvatarCharacter? value)
    {
        InProgress = true;

        string skin = Avatar.SkinsDict[value!.Value].First();
        var avatar = new Avatar(value.Value, skin);
        if (avatar.IsValidSkinForCharacter())
        {
            await SendSetAvatarReq(avatar);
        }
        else
        {
            // todo warn error
        }

        InProgress = false;
    }

    private async Task OnSelectedSkinChanged(string? value)
    {
        InProgress = true;

        var avatar = new Avatar(ClientAvatar!.Character, value!);
        if (avatar.IsValidSkinForCharacter())
        {
            await SendSetAvatarReq(avatar);
        }
        else
        {
            // todo warn error
        }

        InProgress = false;
    }

    private async Task SendSetAvatarReq(Avatar avatar)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/SetAvatar", avatar);
        if (res.IsSuccessStatusCode)
        {
            ClientAvatar = await res.Content.ReadFromJsonAsync<Avatar>();
            ClientState.Session!.Player.Avatar = ClientAvatar!;
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task SendSetDonorBenefitReq(DonorBenefit donorBenefit)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/SetDonorBenefit", donorBenefit);
        if (res.IsSuccessStatusCode)
        {
            ClientDonorBenefit = await res.Content.ReadFromJsonAsync<DonorBenefit>();
            ClientState.Session!.Player.DonorBenefit = ClientDonorBenefit!;
            await _jsRuntime.InvokeVoidAsync("alert", "OK.");
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task OnclickCalculateStats()
    {
        var req = new ReqGetLabelStats(SelectedPresetName, SelectedSSSTM);
        HttpResponseMessage res = await _client.PostAsJsonAsync("Library/GetLabelStats", req);
        if (res.IsSuccessStatusCode)
        {
            LabelStats[SelectedPresetName] = await res.Content.ReadFromJsonAsync<LabelStats>();
        }
        else
        {
            LabelStats[SelectedPresetName] = null;
        }
    }

    private async Task AddEMQVndbInfo()
    {
        InProgress = true;
        var vndbInfo = new PlayerVndbInfo()
        {
            VndbId = $"eu{ClientState.Session!.Player.Id}",
            DatabaseKind = UserListDatabaseKind.EMQ,
            Labels = new List<Label>()
            {
                new()
                {
                    Id = 1,
                    IsPrivate = false,
                    Name = "My EMQ Label",
                    VNs = new Dictionary<string, int>(),
                    Kind = LabelKind.Include
                }
            }
        };
        ClientState.VndbInfo.Add(vndbInfo);

        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/SetVndbInfo",
            new ReqSetVndbInfo(ClientState.Session.Token, ClientState.VndbInfo));
        if (res.IsSuccessStatusCode)
        {
            ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<List<PlayerVndbInfo>>())!;
        }
        else
        {
            InProgress = false;
            throw new Exception();
        }

        InProgress = false;
    }

    private async Task RemoveEMQVndbInfo(PlayerVndbInfo vndbInfo)
    {
        bool confirmed =
            await _jsRuntime.InvokeAsync<bool>("confirm", "Really remove this list and all the sources it contains?");
        if (!confirmed)
        {
            return;
        }

        InProgress = true;

        ClientState.VndbInfo.Remove(vndbInfo);
        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/SetVndbInfo",
            new ReqSetVndbInfo(ClientState.Session!.Token, ClientState.VndbInfo));
        if (res.IsSuccessStatusCode)
        {
            ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<List<PlayerVndbInfo>>())!;
        }
        else
        {
            InProgress = false;
            throw new Exception();
        }

        InProgress = false;
    }

    public async Task FetchMissingSongSourcesForEMQTab()
    {
        var label = ClientState.VndbInfo
            .FirstOrDefault(x => x.DatabaseKind == UserListDatabaseKind.EMQ)?.Labels?.SingleOrDefault();
        if (label == null)
        {
            return;
        }

        var msIds = label.VNs.Select(x => Convert.ToInt32(x.Key.Replace("https://erogemusicquiz.com/ems", "")));
        int[] missing = msIds.Except(ClientState.SourcesCache.Keys).ToArray();
        if (missing.Any())
        {
            var res = await _client.PostAsJsonAsync("Library/GetSongSources",
                missing.Select(x => new SongSource() { Id = x }).ToArray());
            if (res.IsSuccessStatusCode)
            {
                var content = (await res.Content.ReadFromJsonAsync<SongSource[]>())!;
                foreach (SongSource songSource in content)
                {
                    ClientState.SourcesCache[songSource.Id] = songSource;
                }
            }
        }
    }

    public async Task SelectedResultChangedMst()
    {
        var vndbInfo = ClientState.VndbInfo.FirstOrDefault(x => x.DatabaseKind == UserListDatabaseKind.EMQ);
        if (vndbInfo == null)
        {
            return;
        }

        InProgress = true;
        if (!string.IsNullOrWhiteSpace(selectedMusicSourceTitle))
        {
            var req = new ReqFindSongsBySongSourceTitle(selectedMusicSourceTitle);
            var res = await _client.PostAsJsonAsync("Library/FindSongsBySongSourceTitle", req);
            if (res.IsSuccessStatusCode)
            {
                List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                if (songs != null && songs.Any())
                {
                    string norm = selectedMusicSourceTitle.NormalizeForAutocomplete();
                    var label = vndbInfo.Labels!.Single();
                    var current = label.VNs.Keys.Select(x =>
                        Convert.ToInt32(x.Replace("https://erogemusicquiz.com/ems", "")));
                    var msIds = new HashSet<int>(current);
                    foreach (Song song in songs)
                    {
                        foreach (SongSource source in song.Sources)
                        {
                            if (source.Titles.Any(x =>
                                    x.LatinTitle.NormalizeForAutocomplete() == norm ||
                                    x.NonLatinTitle?.NormalizeForAutocomplete() == norm))
                            {
                                msIds.Add(source.Id);
                            }
                        }
                    }

                    foreach (int msId in msIds)
                    {
                        label.VNs[$"https://erogemusicquiz.com/ems{msId}"] = -1;

                        HttpResponseMessage resUpdateLabel = await _client.PostAsJsonAsync("Auth/UpdateLabel",
                            new ReqUpdateLabel(ClientState.Session!.Token, label, vndbInfo.DatabaseKind));
                        if (resUpdateLabel.IsSuccessStatusCode)
                        {
                            var updatedLabel = await resUpdateLabel.Content.ReadFromJsonAsync<Label>();
                            if (updatedLabel != null)
                            {
                                Label oldLabel = ClientState.VndbInfo
                                    .First(x => x.DatabaseKind == vndbInfo.DatabaseKind).Labels!
                                    .Single(x => x.Id == updatedLabel.Id);
                                oldLabel.Kind = updatedLabel.Kind;
                                oldLabel.VNs = updatedLabel.VNs;
                            }

                            await _guessInputComponentRef.ClearInputField();
                        }
                        else
                        {
                            // todo warn user & restore old label state
                            InProgress = false;
                            StateHasChanged();
                            throw new Exception();
                        }
                    }

                    await FetchMissingSongSourcesForEMQTab();
                }
            }
        }

        InProgress = false;
        StateHasChanged();
    }

    private async Task DeleteEMQSourceFromLabel(Label label, string key)
    {
        InProgress = true;

        label.VNs.Remove(key);
        HttpResponseMessage resUpdateLabel = await _client.PostAsJsonAsync("Auth/UpdateLabel",
            new ReqUpdateLabel(ClientState.Session!.Token, label, UserListDatabaseKind.EMQ));
        if (resUpdateLabel.IsSuccessStatusCode)
        {
            var updatedLabel = await resUpdateLabel.Content.ReadFromJsonAsync<Label>();
            if (updatedLabel != null)
            {
                Label oldLabel = ClientState.VndbInfo
                    .First(x => x.DatabaseKind == UserListDatabaseKind.EMQ).Labels!
                    .Single(x => x.Id == updatedLabel.Id);
                oldLabel.Kind = updatedLabel.Kind;
                oldLabel.VNs = updatedLabel.VNs;
            }
        }
        else
        {
            // todo warn user & restore old label state
            InProgress = false;
            StateHasChanged();
            throw new Exception();
        }

        InProgress = false;
    }
}
