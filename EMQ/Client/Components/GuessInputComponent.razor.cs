using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Abstract;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class GuessInputComponent : IAutocompleteComponent
{
    public MyAutocompleteComponent<AutocompleteMst> AutocompleteComponent { get; set; } = null!;

    private AutocompleteMst[] AutocompleteData { get; set; } = Array.Empty<AutocompleteMst>();

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

    private AutocompleteMst? _guessT;

    [Parameter]
    public AutocompleteMst? GuessT
    {
        get => _guessT;
        set
        {
            if (_guessT != value)
            {
                _guessT = value;
                GuessTChanged.InvokeAsync(value);
            }
        }
    }

    [Parameter]
    public EventCallback<string?> GuessChanged { get; set; }

    [Parameter]
    public EventCallback<AutocompleteMst?> GuessTChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    public string? GetSelectedText() => AutocompleteComponent.SelectedText;

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<AutocompleteMst[]>("autocomplete/mst.json"))!;
    }

    public void CallStateHasChanged()
    {
        StateHasChanged();
    }

    public async Task ClearInputField()
    {
#pragma warning disable CS4014
        AutocompleteComponent.Clear(false); // awaiting this causes signalr messages not to be processed in time (???)
        // todo test if that is still true
#pragma warning restore CS4014
        Guess = "";
        GuessT = null;
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
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        const int maxResults = 25; // todo
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();
        foreach (AutocompleteMst d in AutocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan()
                .StartsWithContains(valueSpan, StringComparison.Ordinal);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan()
                    .StartsWithContains(valueSpan, StringComparison.Ordinal);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return (TValue[])(object)dictLT.Concat(dictNLT)
            .OrderByDescending(x => x.Value)
            .DistinctBy(x => x.Key.MSTLatinTitle)
            .Take(maxResults)
            .Select(x => x.Key)
            .ToArray();
    }

    private async Task OnValueChanged(AutocompleteMst? value)
    {
        string s = value != null ? value.MSTLatinTitle : AutocompleteComponent.SelectedText;
        if (string.IsNullOrEmpty(Guess) && string.IsNullOrEmpty(s))
        {
            return;
        }

        Guess = s;
        GuessT = value;
        // Console.WriteLine(Guess);

        if (IsQuizPage)
        {
            // todo do this with callback
            await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", Guess, GuessKind.Mst);
            if (ClientState.Preferences.AutoSkipGuessPhase)
            {
                // todo dedup
                // todo EnabledGuessKinds stuff
                await ClientState.Session!.hubConnection!.SendAsync("SendToggleSkip");
                StateHasChanged();
            }
        }

        Callback?.Invoke();
    }
}
