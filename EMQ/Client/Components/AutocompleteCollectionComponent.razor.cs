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
using EMQ.Shared.Quiz.Entities.Abstract;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class AutocompleteCollectionComponent : IAutocompleteComponent
{
    public MyAutocompleteComponent<AutocompleteCollection> AutocompleteComponent { get; set; } = null!;

    public static AutocompleteCollection[] AutocompleteData { get; set; } = Array.Empty<AutocompleteCollection>();

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public bool IsDisabled { get; set; }

    [Parameter]
    public bool IsQuizPage { get; set; }

    private AutocompleteCollection? _guess;

    [Parameter]
    public AutocompleteCollection? Guess
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
    public EventCallback<AutocompleteCollection?> GuessChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData =
            (await _client.GetFromJsonAsync<AutocompleteCollection[]>("autocomplete/collection.json", Utils.Jso))!;
    }

    public void CallStateHasChanged()
    {
        StateHasChanged();
    }

    public string GetSelectedText() => AutocompleteComponent.SelectedText;

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

    private TValue[] OnSearch<TValue>(string value)
    {
        value = value.NormalizeForAutocomplete();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<TValue>();
        }

        const int maxResults = 25; // todo
        var dictLT = new Dictionary<AutocompleteCollection, StringMatch>();
        var valueSpan = value.AsSpan();
        foreach (AutocompleteCollection d in AutocompleteData)
        {
            var matchLT = d.Name.NormalizeForAutocomplete().AsSpan()
                .StartsWithContains(valueSpan, StringComparison.Ordinal);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }
        }

        return (TValue[])(object)dictLT
            .OrderByDescending(x => x.Value)
            // .DistinctBy(x => x.Key.VndbId)
            .Take(maxResults)
            .Select(x => x.Key)
            .ToArray();
    }

    public AutocompleteCollection? MapValue(AutocompleteCollection? value)
    {
        string s = value != null ? value.Name : AutocompleteComponent.SelectedText;
        if (string.IsNullOrEmpty(Guess?.Name) && string.IsNullOrEmpty(s))
        {
            return null;
        }

        return value ?? new AutocompleteCollection { Name = s };
    }

    private async Task OnValueChanged(AutocompleteCollection? value)
    {
        Guess = MapValue(value);
        Callback?.Invoke();
    }
}
