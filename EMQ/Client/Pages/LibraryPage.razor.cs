using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.Web;

namespace EMQ.Client.Pages;

public partial class LibraryPage
{
    public class AddSongLinkModel
    {
        [Required]
        [RegularExpression(RegexPatterns.SongLinkUrlRegex, ErrorMessage = "Invalid Url")]
        public string Url { get; set; } = "";
    }

    private AddSongLinkModel _addSongLinkModel = new();

    private string[] AutocompleteData { get; set; } = Array.Empty<string>();

    private ReviewQueueComponent? _reviewQueueComponent { get; set; }

    public string? selectedMusicSourceTitle { get; set; }

    public List<Song> CurrentSongs { get; set; } = new();

    public string NoSongsText { get; set; } = "";

    public bool ShowUploadCriteria { get; set; }

    public IEnumerable<string> currentDataSource = Array.Empty<string>();

    public Autocomplete<string, string> AutocompleteComponent { get; set; } = null!;

    public int ActiveSongId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        AutocompleteData = (await _client.GetFromJsonAsync<string[]>("autocomplete_mst.json"))!;
    }

    private async Task SelectedResultChanged()
    {
        if (!string.IsNullOrWhiteSpace(selectedMusicSourceTitle))
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var req = new ReqFindSongsBySongSourceTitle(selectedMusicSourceTitle);
            var res = await _client.PostAsJsonAsync("Library/FindSongsBySongSourceTitle", req);
            if (res.IsSuccessStatusCode)
            {
                List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                if (songs != null && songs.Any())
                {
                    CurrentSongs = songs;
                }
                else
                {
                    var req2 = new ReqFindSongsByArtistTitle(selectedMusicSourceTitle);
                    var res2 = await _client.PostAsJsonAsync("Library/FindSongsByArtistTitle", req2);
                    if (res.IsSuccessStatusCode)
                    {
                        List<Song>? songs2 = await res2.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                        if (songs2 != null && songs2.Any())
                        {
                            CurrentSongs = songs2;
                            selectedMusicSourceTitle = null;
                        }
                    }
                }

                if (!CurrentSongs.Any())
                {
                    NoSongsText = "No results.";
                }

                StateHasChanged();
            }
        }
    }

    private async Task SubmitSongUrl(int mId, string url)
    {
        if (ClientState.Session?.Player.Username is null)
        {
            return;
        }

        _addSongLinkModel.Url = "";
        StateHasChanged();

        url = url.Trim().ToLowerInvariant();
        bool isVideo = url.IsVideoLink();
        SongLinkType songLinkType = url.Contains("catbox") ? SongLinkType.Catbox : SongLinkType.Unknown;

        var submittedBy = !string.IsNullOrEmpty(ClientState.Session.VndbInfo.VndbId)
            ? ClientState.Session.VndbInfo.VndbId
            : ClientState.Session.Player.Username;
        var req = new ReqImportSongLink(mId, new SongLink() { Url = url, IsVideo = isVideo, Type = songLinkType },
            submittedBy);
        var res = await _client.PostAsJsonAsync("Library/ImportSongLink", req);
        if (res.IsSuccessStatusCode)
        {
            var isSuccess = await res.Content.ReadFromJsonAsync<bool>();
            if (isSuccess)
            {
                Console.WriteLine("Imported song link!");
                await _reviewQueueComponent!.RefreshRQs();
            }
            else
            {
                // todo show error
                Console.WriteLine("Error importing song link");
            }
        }
    }

    private void OnHandleReadData(AutocompleteReadDataEventArgs autocompleteReadDataEventArgs)
    {
        if (!autocompleteReadDataEventArgs.CancellationToken.IsCancellationRequested)
        {
            currentDataSource =
                Autocomplete.SearchAutocompleteMst(AutocompleteData, autocompleteReadDataEventArgs.SearchValue);
        }
    }

    private bool CustomFilter(string item, string searchValue)
    {
        return true;
    }

    private async Task Onkeypress(KeyboardEventArgs obj)
    {
        if (obj.Key is "Enter" or "NumpadEnter")
        {
            if (selectedMusicSourceTitle != AutocompleteComponent.SelectedText)
            {
                selectedMusicSourceTitle = AutocompleteComponent.SelectedText;
                await AutocompleteComponent.Close();
                StateHasChanged();
                await SelectedResultChanged();
            }
        }
    }

    private void SelectedValueChanged(string arg)
    {
        currentDataSource = new List<string> { arg }; // work-around for an issue I'm too lazy to submit a report for
    }

    private void ChangeActiveSong(int songId)
    {
        _addSongLinkModel.Url = "";
        if (songId == ActiveSongId)
        {
            ActiveSongId = 0;
        }
        else
        {
            ActiveSongId = songId;
        }

        StateHasChanged();
    }
}
