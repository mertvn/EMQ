using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Blazorise.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class AutocompleteCComponent
{
    public MyAutocompleteComponent<SongSourceCategory> AutocompleteComponent { get; set; } = null!;

    public SongSourceCategory[] AutocompleteData { get; set; } = Array.Empty<SongSourceCategory>();

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public bool IsDisabled { get; set; }

    [Parameter]
    public bool IsQuizPage { get; set; }

    private SongSourceCategory? _guess;

    [Parameter]
    public SongSourceCategory? Guess
    {
        get => _guess;
        set
        {
            if (_guess != value)
            {
                _guess = value;
                GuessChanged.InvokeAsync(value);
            }
        }
    }

    [Parameter]
    public EventCallback<SongSourceCategory?> GuessChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<SongSourceCategory[]>("autocomplete/c.json", Utils.Jso))!;
    }

    public void CallStateHasChanged()
    {
        StateHasChanged();
    }

    public async Task ClearInputField()
    {
#pragma warning disable CS4014
        AutocompleteComponent.Clear(false); // awaiting this causes signalr messages not to be processed in time (???)
#pragma warning restore CS4014
        await Task.Delay(100);
        StateHasChanged();
    }

    public void CallClose()
    {
        AutocompleteComponent.Close();
    }

    private TValue[] OnSearch<TValue>(string value)
    {
        value = value.NormalizeForAutocomplete();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<TValue>();
        }

        const int maxResults = 25; // todo
        var dictLT = new Dictionary<SongSourceCategory, StringMatch>();
        var dictNLT = new Dictionary<SongSourceCategory, StringMatch>();
        foreach (SongSourceCategory d in AutocompleteData)
        {
            var matchLT = d.Name.NormalizeForAutocomplete().StartsWithContains(value, StringComparison.Ordinal);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (!string.IsNullOrEmpty(d.VndbId))
            {
                var matchNLT = d.VndbId.StartsWithContains(value, StringComparison.Ordinal);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return (TValue[])(object)dictLT.Concat(dictNLT)
            .OrderByDescending(x => x.Value)
            // .DistinctBy(x => x.Key.VndbId)
            .Take(maxResults)
            .Select(x => x.Key)
            .ToArray();
    }

    public SongSourceCategory? MapValue(SongSourceCategory? value)
    {
        string s = value != null ? value.Name : AutocompleteComponent.SelectedText;
        if (string.IsNullOrEmpty(Guess?.Name) && string.IsNullOrEmpty(s))
        {
            return null;
        }

        return value ?? new SongSourceCategory { Name = s };
    }

    private async Task OnValueChanged(SongSourceCategory? value)
    {
        Guess = MapValue(value);
        // Console.WriteLine(Guess);

        // if (IsQuizPage)
        // {
        //     // todo do this with callback
        //     await ClientState.Session!.hubConnection!.SendAsync("SendGuessChangedC", Guess);
        // }

        Callback?.Invoke();
    }
}
