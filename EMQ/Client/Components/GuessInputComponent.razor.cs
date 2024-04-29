using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.Components;
using EMQ.Shared.Library.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class GuessInputComponent
{
    private AutocompleteMst[] AutocompleteData { get; set; } = Array.Empty<AutocompleteMst>();

    private IEnumerable<string> CurrentDataSource { get; set; } = Array.Empty<string>();

    public Autocomplete<string, string> AutocompleteComponent { get; set; } = null!;

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public bool FreeTyping { get; set; }

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
    public EventCallback<string?> GuessChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    // todo consider using a LRU cache
    private Dictionary<string, string[]> Cache { get; set; } = new();

    public string? GetSelectedText() => AutocompleteComponent.SelectedText;

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<AutocompleteMst[]>("autocomplete/mst.json"))!;
    }

    private void OnHandleReadData(AutocompleteReadDataEventArgs autocompleteReadDataEventArgs)
    {
        if (!autocompleteReadDataEventArgs.CancellationToken.IsCancellationRequested)
        {
            if (Cache.TryGetValue(autocompleteReadDataEventArgs.SearchValue, out var r))
            {
                CurrentDataSource = r;
            }
            else
            {
                string[] res = Autocomplete
                    .SearchAutocompleteMst(AutocompleteData, autocompleteReadDataEventArgs.SearchValue).ToArray();
                Cache[autocompleteReadDataEventArgs.SearchValue] = res;
                CurrentDataSource = res;
            }
        }
    }

    private bool CustomFilter(string item, string searchValue)
    {
        return true;
    }

    public void CallStateHasChanged()
    {
        StateHasChanged();
    }

    public async Task ClearInputField()
    {
#pragma warning disable CS4014
        AutocompleteComponent.Clear(); // awaiting this causes signalr messages not to be processed in time (???)
#pragma warning restore CS4014
        Guess = "";
        await Task.Delay(100);
        StateHasChanged();
    }

    private async Task Onkeypress(KeyboardEventArgs obj)
    {
        if (obj.Key is "Enter" or "NumpadEnter")
        {
            // todo important find another way to prevent spam
            // if (Guess != AutocompleteComponent.SelectedText)
            // {
            Guess = AutocompleteComponent.SelectedText;
            await AutocompleteComponent.Close();
            StateHasChanged();

            if (IsQuizPage)
            {
                // todo do this with callback

                await ClientState.Session!.hubConnection!.SendAsync("SendGuessChangedMst", Guess);
                if (ClientState.Session!.Player.Preferences.AutoSkipGuessPhase)
                {
                    // todo dedup
                    // todo EnabledGuessKinds stuff
                    await ClientState.Session!.hubConnection!.SendAsync("SendToggleSkip");
                    StateHasChanged();
                }
            }

            Callback?.Invoke();
            // }
        }
    }

    private void SelectedValueChanged(string arg)
    {
        // work-around for an issue I'm too lazy to submit a report for
        if (Cache.TryGetValue(arg, out var r))
        {
            CurrentDataSource = r;
        }
        else
        {
            string[] res = Autocomplete.SearchAutocompleteMst(AutocompleteData, arg).ToArray();
            Cache[arg] = res;
            CurrentDataSource = res;
        }
    }

    public void CallClose()
    {
        AutocompleteComponent.Close();
    }

    public async Task CallFocusAsync()
    {
        await AutocompleteComponent.Focus();
    }
}
