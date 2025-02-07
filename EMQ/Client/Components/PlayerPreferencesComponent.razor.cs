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

    public bool InProgress { get; set; }

    public List<string> LoginProgressDisplay { get; set; } = new();

    private string? ClientVndbApiToken { get; set; }

    public List<UserLabelPreset> Presets { get; set; } = new();

    public string SelectedPresetName { get; set; } = "";

    // 1 more char. than the longest preset name allowed
    public const string CreateNewPresetValue = "-----------------------------------------------------------------";

    public Avatar? ClientAvatar { get; set; }

    public Dictionary<string, LabelStats?> LabelStats { get; set; } = new();

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

    private async Task FetchLabels(PlayerVndbInfo vndbInfo)
    {
        // LoginInProgress = true;
        // StateHasChanged();

        var final = await FetchLabelsInner(vndbInfo);
        ClientState.VndbInfo.Labels = final;

        // LoginInProgress = false;
        StateHasChanged();
    }

    public async Task<List<Label>> FetchLabelsInner(PlayerVndbInfo vndbInfo)
    {
        List<Label> newLabels = new();

        VNDBLabel[] vndbLabels = await VndbMethods.GetLabels(vndbInfo);
        foreach (VNDBLabel vndbLabel in vndbLabels)
        {
            newLabels.Add(Label.FromVndbLabel(vndbLabel));
        }

        Console.WriteLine(vndbInfo.Labels!.Count + "-" + newLabels.Count);
        List<Label> final = Label.MergeLabels(vndbInfo.Labels!, newLabels);
        return final;
    }

    private async Task OnLabelKindChanged(Label label, LabelKind newLabelKind)
    {
        if (label.Kind == newLabelKind)
        {
            return;
        }

        label = await UpdateLabel(label, newLabelKind);

        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/UpdateLabel",
            new ReqUpdateLabel(ClientState.Session!.Token, label));

        if (res.IsSuccessStatusCode)
        {
            var updatedLabel = await res.Content.ReadFromJsonAsync<Label>();
            if (updatedLabel != null)
            {
                Label oldLabel = ClientState.VndbInfo.Labels!.Single(x => x.Id == updatedLabel.Id);
                oldLabel.Kind = updatedLabel.Kind;
                oldLabel.VNs = updatedLabel.VNs;
            }
        }
        else
        {
            // todo warn user & restore old label state
        }
    }

    public async Task<Label> UpdateLabel(Label label, LabelKind newLabelKind)
    {
        if (ClientState.Session != null && ClientState.VndbInfo.Labels != null)
        {
            Console.WriteLine(
                $"{ClientState.VndbInfo.VndbId}: {label.Id} ({label.Name}), {label.Kind} => {newLabelKind}");
            label.Kind = newLabelKind;

            var newVns = new Dictionary<string, int>();
            switch (label.Kind)
            {
                case LabelKind.Maybe:
                    break;
                case LabelKind.Include:
                case LabelKind.Exclude:
                    var grabbed = await VndbMethods.GrabPlayerVNsFromVndb(new PlayerVndbInfo()
                    {
                        VndbId = ClientState.VndbInfo.VndbId,
                        VndbApiToken = ClientVndbApiToken,
                        Labels = new List<Label>() { label },
                    });
                    newVns = grabbed.SingleOrDefault()?.VNs ?? new Dictionary<string, int>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            label.VNs = newVns;
            return label;
        }
        else
        {
            // should never be hit under normal circumstances
            throw new Exception();
        }
    }

    // public async Task GetVndbInfoFromServer(PlayerVndbInfo vndbInfo)
    // {
    //     vndbInfo.Labels = new List<Label>();
    //
    //     HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/GetVndbInfo",
    //         new ReqSetVndbInfo(ClientState.Session!.Token, new PlayerVndbInfo()));
    //
    //     if (res.IsSuccessStatusCode)
    //     {
    //         var content = await res.Content.ReadFromJsonAsync<PlayerVndbInfo>();
    //         ClientState.VndbInfo = content!;
    //     }
    //     else
    //     {
    //         // todo warn user
    //
    //         // todo?
    //         // Labels.Clear();
    //     }
    //
    //     StateHasChanged();
    // }

    public async Task SetVndbInfo(PlayerVndbInfo vndbInfo)
    {
        InProgress = true;
        LoginProgressDisplay.Clear();
        StateHasChanged();

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

        if (string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            vndbInfo.VndbId = "";
            ClientVndbApiToken = "";
            vndbInfo.Labels = new List<Label>();
        }
        else
        {
            vndbInfo.Labels = new List<Label>();
            vndbInfo.VndbApiToken = ClientVndbApiToken;
            await FetchLabels(vndbInfo);

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

            var vns = await VndbMethods.GrabPlayerVNsFromVndb(vndbInfo);
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
            new ReqSetVndbInfo(ClientState.Session!.Token, vndbInfo));

        if (res.IsSuccessStatusCode)
        {
            ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<PlayerVndbInfo>())!;
        }
        else
        {
            // todo warn user
            ClientState.VndbInfo.Labels = new List<Label>();
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
                        ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<PlayerVndbInfo>())!;
                    }
                    else
                    {
                        // todo warn user
                        SelectedPresetName = "";
                        ClientState.VndbInfo.Labels = new List<Label>();
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
                ClientState.VndbInfo = (await res.Content.ReadFromJsonAsync<PlayerVndbInfo>())!;
            }
            else
            {
                // todo warn user
                SelectedPresetName = "";
                ClientState.VndbInfo.Labels = new List<Label>();
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
            ClientState.VndbInfo = new PlayerVndbInfo();
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
        }
        else
        {
            // todo warn error
        }
    }

    private async Task OnclickCalculateStats()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Library/GetLabelStats", SelectedPresetName);
        if (res.IsSuccessStatusCode)
        {
            LabelStats[SelectedPresetName] = await res.Content.ReadFromJsonAsync<LabelStats>();
        }
        else
        {
            LabelStats[SelectedPresetName] = null;
        }
    }
}
