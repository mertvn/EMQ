using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client.Pages;

public partial class GuessInputComponent
{
    private string[] AutocompleteData { get; set; } = Array.Empty<string>();

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

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<string[]>("autocomplete_mst.json"))!;
    }

    private void OnHandleReadData(AutocompleteReadDataEventArgs autocompleteReadDataEventArgs)
    {
        if (!autocompleteReadDataEventArgs.CancellationToken.IsCancellationRequested)
        {
            CurrentDataSource =
                Autocomplete.SearchAutocompleteMst(AutocompleteData, autocompleteReadDataEventArgs.SearchValue);
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
            if (Guess != AutocompleteComponent.SelectedText)
            {
                Guess = AutocompleteComponent.SelectedText;
                await AutocompleteComponent.Close();
                StateHasChanged();

                if (IsQuizPage)
                {
                    await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", Guess);
                }

                Callback?.Invoke();
            }
        }
    }

    private void SelectedValueChanged(string arg)
    {
        CurrentDataSource = new List<string> { arg }; // work-around for an issue I'm too lazy to submit a report for
    }

    public void CallClose()
    {
        AutocompleteComponent.Close();
    }
}
