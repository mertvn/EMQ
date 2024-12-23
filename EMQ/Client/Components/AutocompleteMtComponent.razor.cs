using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Blazorise.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Abstract;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class AutocompleteMtComponent : IAutocompleteComponent
{
    public MyAutocompleteComponent<string> AutocompleteComponent { get; set; } = null!;

    public AutocompleteMt[] AutocompleteData { get; set; } = Array.Empty<AutocompleteMt>();

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public bool IsDisabled { get; set; }

    [Parameter]
    public bool IsQuizPage { get; set; }

    private string? _guess;

    [Parameter]
    public string? Guess
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
    public EventCallback<string> GuessChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    public string? GetSelectedText() => AutocompleteComponent.SelectedText;

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<AutocompleteMt[]>("autocomplete/mt.json"))!;
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

    public async Task CallFocusAsync()
    {
        await AutocompleteComponent.Focus();
    }

    private TValue[] OnSearch<TValue>(string value)
    {
        value = value.NormalizeForAutocomplete();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<TValue>();
        }

        var valueSpan = value.AsSpan();
        // bool hasNonAscii = !Ascii.IsValid(valueSpan);
        const int maxResults = 25; // todo
        var dictLT = new Dictionary<AutocompleteMt, StringMatch>();
        // var dictNLT = new Dictionary<AutocompleteMt, StringMatch>();
        foreach (AutocompleteMt d in AutocompleteData)
        {
            var matchLT = d.MTLatinTitleNormalized.AsSpan().StartsWithContains(valueSpan, StringComparison.Ordinal);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            // if (hasNonAscii)
            // {
            //     var matchNLT = d.MTNonLatinTitleNormalized.StartsWithContains(value, StringComparison.Ordinal);
            //     if (matchNLT > 0)
            //     {
            //         dictNLT[d] = matchNLT;
            //     }
            // }
        }

        return (TValue[])(object)dictLT
            .OrderByDescending(x => x.Value)
            .DistinctBy(x => x.Key.MTLatinTitle)
            .Take(maxResults)
            .Select(x => x.Key.MTLatinTitle)
            .ToArray();
    }

    private async Task OnValueChanged(string? value)
    {
        string s = value ?? AutocompleteComponent.SelectedText;
        if (string.IsNullOrEmpty(Guess) && string.IsNullOrEmpty(s))
        {
            return;
        }

        Guess = s;
        // Console.WriteLine(Guess);

        if (IsQuizPage)
        {
            // todo do this with callback
            await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", Guess, GuessKind.Mt);
        }

        Callback?.Invoke();
    }
}
