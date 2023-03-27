using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
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
            // todo
        }
    }

    public async Task<Label> UpdateLabel(Label label, LabelKind newLabelKind)
    {
        if (ClientState.Session != null)
        {
            if (ClientState.Session.VndbInfo.Labels != null)
            {

                Console.WriteLine($"{ClientState.Session.VndbInfo.VndbId}: " + label.Id + ", " + label.Kind + " => " +
                                  newLabelKind);
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
                // todo
                throw new Exception();
            }
        }
        else
        {
            // todo
            throw new Exception();
        }
    }
}
