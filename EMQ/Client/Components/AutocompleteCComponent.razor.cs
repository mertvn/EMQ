using System;
using System.Collections.Generic;
using System.Net.Http.Json;
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
    public SongSourceCategory[] AutocompleteData { get; set; } = Array.Empty<SongSourceCategory>();

    private IEnumerable<SongSourceCategory> CurrentDataSource { get; set; } = Array.Empty<SongSourceCategory>();

    public Autocomplete<SongSourceCategory, SongSourceCategory> AutocompleteComponent { get; set; } = null!;

    [Parameter]
    public string Placeholder { get; set; } = "";

    [Parameter]
    public bool FreeTyping { get; set; }

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

    private void OnHandleReadData(AutocompleteReadDataEventArgs autocompleteReadDataEventArgs)
    {
        if (!autocompleteReadDataEventArgs.CancellationToken.IsCancellationRequested)
        {
            CurrentDataSource =
                Autocomplete.SearchAutocompleteC(AutocompleteData, autocompleteReadDataEventArgs.SearchValue);
        }
    }

    private bool CustomFilter(SongSourceCategory item, string searchValue)
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
        await Task.Delay(100);
        StateHasChanged();
    }

    private async Task Onkeypress(KeyboardEventArgs obj)
    {
        if (obj.Key is "Enter" or "NumpadEnter")
        {
            if (Guess != AutocompleteComponent.SelectedValue)
            {
                Guess = AutocompleteComponent.SelectedValue;
                await AutocompleteComponent.Close();
                StateHasChanged();

                if (IsQuizPage)
                {
                    // todo do this with callback
                    await ClientState.Session!.hubConnection!.SendAsync("SendGuessChangedC", Guess);
                }

                Callback?.Invoke();
            }
        }
    }

    private void SelectedValueChanged(SongSourceCategory arg)
    {
        CurrentDataSource =
            Autocomplete.SearchAutocompleteC(AutocompleteData,
                arg.Name); // work-around for an issue I'm too lazy to submit a report for
    }

    public void CallClose()
    {
        AutocompleteComponent.Close();
    }
}
