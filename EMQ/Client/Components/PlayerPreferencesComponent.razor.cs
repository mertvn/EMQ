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
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Juliet.Model.VNDBObject;

namespace EMQ.Client.Components;

public partial class PlayerPreferencesComponent
{
    private Blazorise.Modal _modalRef = null!;

    private string _selectedTab = "TabGeneral";

    private List<Label> Labels { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        if (ClientState.Session?.VndbInfo.Labels != null)
        {
            Labels = ClientState.Session.VndbInfo.Labels.ToList();
        }
    }

    private async Task UpdatePlayerPreferences(PlayerPreferences playerPreferencesModel)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/UpdatePlayerPreferences",
            new ReqUpdatePlayerPreferences(ClientState.Session!.Token, playerPreferencesModel));

        if (res.IsSuccessStatusCode)
        {
            ClientState.Session.Player.Preferences = (await res.Content.ReadFromJsonAsync<PlayerPreferences>())!;
            await _clientUtils.SaveSessionToLocalStorage();
            await _modalRef.Hide();
        }
        else
        {
        }

        StateHasChanged();
    }

    public void OnclickButtonPreferences()
    {
        StateHasChanged();
        if (ClientState.Session != null)
        {
            _modalRef.Show();
        }
    }

    private Task OnSelectedTabChanged(string name)
    {
        _selectedTab = name;
        return Task.CompletedTask;
    }

    private async Task FetchLabels(PlayerVndbInfo vndbInfo)
    {
        Labels.Clear();
        List<Label> newLabels = new();

        VNDBLabel[] vndbLabels = await VndbMethods.GetLabels(vndbInfo);
        foreach (VNDBLabel vndbLabel in vndbLabels)
        {
            newLabels.Add(Label.FromVndbLabel(vndbLabel));
        }

        Console.WriteLine(vndbInfo.Labels!.Count + "-" + newLabels.Count);
        List<Label> final = Label.MergeLabels(vndbInfo.Labels!, newLabels);
        Labels.AddRange(final);
        StateHasChanged();
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
                Label oldLabel = Labels.Single(x => x.Id == updatedLabel.Id);
                oldLabel.Kind = updatedLabel.Kind;
                oldLabel.VNs = updatedLabel.VNs;

                await _clientUtils.SaveSessionToLocalStorage();
            }
        }
        else
        {
            // todo warn user & restore old label state
        }
    }

    public async Task<Label> UpdateLabel(Label label, LabelKind newLabelKind)
    {
        if (ClientState.Session != null)
        {
            if (ClientState.Session.VndbInfo.Labels != null)
            {
                Console.WriteLine(
                    $"{ClientState.Session.VndbInfo.VndbId}: {label.Id} ({label.Name}), {label.Kind} => {newLabelKind}");
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
                            VndbId = ClientState.Session.VndbInfo.VndbId,
                            VndbApiToken = ClientState.Session.VndbInfo.VndbApiToken,
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
        else
        {
            // should never be hit under normal circumstances
            throw new Exception();
        }
    }

    private async Task SetVndbInfo(PlayerVndbInfo vndbInfo)
    {
        if (!string.IsNullOrWhiteSpace(vndbInfo.VndbId)
            // && new Regex(RegexPatterns.VndbIdRegex).IsMatch(vndbInfo.VndbId)
           )
        {
            Labels.Clear();
            vndbInfo.Labels = new List<Label>();
            await FetchLabels(vndbInfo);
            vndbInfo.Labels =
                JsonSerializer.Deserialize<List<Label>>(
                    JsonSerializer.Serialize(Labels))!; // need a deep copy

            // we try including playing, finished, stalled, voted, EMQ-wl, and EMQ-bl labels by default
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
            vndbInfo.Labels = vns;

            HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/SetVndbInfo",
                new ReqSetVndbInfo(ClientState.Session!.Token, vndbInfo));

            if (res.IsSuccessStatusCode)
            {
                ClientState.Session.VndbInfo = vndbInfo;
                await FetchLabels(vndbInfo);
            }
            else
            {
                // todo warn user
                Labels.Clear();
            }

            StateHasChanged();
        }
    }
}
