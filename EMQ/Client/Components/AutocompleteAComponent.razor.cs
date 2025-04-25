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

public partial class AutocompleteAComponent : IAutocompleteComponent
{
    public MyAutocompleteComponent<AutocompleteA> AutocompleteComponent { get; set; } = null!;

    public AutocompleteA[] AutocompleteData { get; set; } = Array.Empty<AutocompleteA>();

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public bool IsDisabled { get; set; }

    [Parameter]
    public bool IsQuizPage { get; set; }

    private AutocompleteA? _guess;

    [Parameter]
    public AutocompleteA? Guess
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
    public EventCallback<AutocompleteA?> GuessChanged { get; set; }

    private string? _guessLatin;

    [Parameter]
    public string? GuessLatin
    {
        get => _guessLatin;
        set
        {
            if (_guessLatin != value)
            {
                _guessLatin = value;
                GuessLatinChanged.InvokeAsync(value);
            }
        }
    }

    [Parameter]
    public EventCallback<string?> GuessLatinChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    [Parameter]
    public GuessKind GuessKind { get; set; }

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<AutocompleteA[]>("autocomplete/a.json"))!;
    }

    public void CallStateHasChanged()
    {
        StateHasChanged();
    }

    public string? GetSelectedText() => throw new NotImplementedException();

    public async Task ClearInputField()
    {
#pragma warning disable CS4014
        AutocompleteComponent.Clear(false); // awaiting this causes signalr messages not to be processed in time (???)
#pragma warning restore CS4014
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

    private static string? OnIconField(AutocompleteA t)
    {
        string? str = t.MainRole switch
        {
            SongArtistRole.Unknown => "assets/text/U.svg",
            SongArtistRole.Vocals => "assets/text/V.svg",
            SongArtistRole.Composer => "assets/text/C.svg",
            SongArtistRole.Arranger => "assets/text/A.svg",
            SongArtistRole.Lyricist => "assets/text/L.svg",
            _ => null
        };

        return str;
    }

    private TValue[] OnSearch<TValue>(string value)
    {
        if (value.StartsWith("id:"))
        {
            string replaced = value.Replace("id:", "");
            if (string.IsNullOrWhiteSpace(replaced))
            {
                return Array.Empty<TValue>();
            }

            return (TValue[])(object)new AutocompleteA[] { new(Convert.ToInt32(replaced), replaced) };
        }

        value = value.NormalizeForAutocomplete();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<TValue>();
        }

        var valueSpan = value.AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        const int maxResults = 30; // todo
        var dictLT = new Dictionary<AutocompleteA, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteA, StringMatch>();
        foreach (AutocompleteA d in AutocompleteData)
        {
            var matchLT = d.AALatinAliasNormalized.AsSpan()
                .StartsWithContains(valueSpan, StringComparison.Ordinal);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }
            else if (d.AALatinAliasNormalizedReversed.AsSpan()
                         .StartsWithContains(valueSpan, StringComparison.Ordinal) >
                     0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.AANonLatinAliasNormalized.AsSpan()
                    .StartsWithContains(valueSpan, StringComparison.Ordinal);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
                else if (d.AANonLatinAliasNormalizedReversed.AsSpan()
                             .StartsWithContains(valueSpan, StringComparison.Ordinal) > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return (TValue[])(object)dictLT.Concat(dictNLT)
            .OrderByDescending(x => x.Value).ThenByDescending(x => x.Key.IsMain)
            .DistinctBy(x => x.Key.AId)
            .Take(maxResults)
            .Select(x => x.Key)
            .ToArray();
    }

    public AutocompleteA? MapValue(AutocompleteA? value)
    {
        string s = value != null ? value.AALatinAlias : AutocompleteComponent.SelectedText;
        if (string.IsNullOrEmpty(Guess?.AALatinAlias) && string.IsNullOrEmpty(s))
        {
            return null;
        }

        return value ?? new AutocompleteA { AALatinAlias = s };
    }

    private async Task OnValueChanged(AutocompleteA? value)
    {
        Guess = MapValue(value);
        GuessLatin = Guess?.AALatinAlias;
        // Console.WriteLine(Guess);
        if (IsQuizPage)
        {
            // todo do this with callback
            await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", GuessLatin, GuessKind);
        }

        // todo important fix this everywhere
        if (Callback != null)
        {
            await Callback.Invoke();
        }
    }
}
