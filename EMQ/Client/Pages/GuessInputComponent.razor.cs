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
    [CascadingParameter]
    private QuizPage QuizPage { get; set; } = null!;

    private string[] AutocompleteData { get; set; } = Array.Empty<string>();

    public IEnumerable<string> currentDataSource = Array.Empty<string>();

    public Autocomplete<string, string> AutocompleteComponent { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        AutocompleteData = (await _client.GetFromJsonAsync<string[]>("autocomplete.json"))!;
    }

    private async Task OnHandleReadData(AutocompleteReadDataEventArgs autocompleteReadDataEventArgs)
    {
        if (!autocompleteReadDataEventArgs.CancellationToken.IsCancellationRequested)
        {
            currentDataSource =
                Autocomplete.SearchAutocomplete(autocompleteReadDataEventArgs.SearchValue, AutocompleteData);
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
        AutocompleteComponent.Clear(); // awaiting this causes signalr messages not to be processed in time (???)
        await Task.Delay(100);
        StateHasChanged();
    }

    private async Task Onkeypress(KeyboardEventArgs obj)
    {
        if (obj.Key is "Enter" or "NumpadEnter")
        {
            if (QuizPage.PageState.Guess != AutocompleteComponent.SelectedText)
            {
                QuizPage.PageState.Guess = AutocompleteComponent.SelectedText;
                await AutocompleteComponent.Close();
                StateHasChanged();

                await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", QuizPage.PageState.Guess);
            }
        }
    }

    private async Task SelectedValueChanged(string arg)
    {
        currentDataSource = new List<string> { arg }; // work-around for an issue I'm too lazy to submit a report for
    }

    public void CallClose()
    {
        AutocompleteComponent.Close();
    }
}
