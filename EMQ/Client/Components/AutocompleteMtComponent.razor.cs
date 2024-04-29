using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.Components;
using EMQ.Shared.Library.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Components;

public partial class AutocompleteMtComponent
{
    public AutocompleteMt[] AutocompleteData { get; set; } = Array.Empty<AutocompleteMt>();

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
    public EventCallback<string> GuessChanged { get; set; }

    [Parameter]
    public Func<Task>? Callback { get; set; }

    public string? GetSelectedText() => AutocompleteComponent.SelectedText;

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<AutocompleteMt[]>("autocomplete/mt.json"))!;
    }

    private void OnHandleReadData(AutocompleteReadDataEventArgs autocompleteReadDataEventArgs)
    {
        if (!autocompleteReadDataEventArgs.CancellationToken.IsCancellationRequested)
        {
            CurrentDataSource =
                Autocomplete.SearchAutocompleteMt(AutocompleteData, autocompleteReadDataEventArgs.SearchValue);
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
        await Task.Delay(100);
        StateHasChanged();
    }

    private async Task Onkeypress(KeyboardEventArgs obj)
    {
        if (obj.Key is "Enter" or "NumpadEnter")
        {
            // todo important find another way to prevent spam
            // if (Guess?.MId != AutocompleteComponent.SelectedValue?.MId)
            // {
            Guess = AutocompleteComponent.SelectedValue;
            await AutocompleteComponent.Close();
            StateHasChanged();

            if (IsQuizPage)
            {
                // todo do this with callback
                await ClientState.Session!.hubConnection!.SendAsync("SendGuessChangedMt", Guess);
            }

            Callback?.Invoke();
            // }
        }
    }

    private void SelectedValueChanged(string arg)
    {
        CurrentDataSource =
            Autocomplete.SearchAutocompleteMt(AutocompleteData,
                arg); // work-around for an issue I'm too lazy to submit a report for
    }

    public void CallClose()
    {
        AutocompleteComponent.Close();
    }
}
