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

        label.Kind = newLabelKind;
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
}
