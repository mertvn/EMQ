using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Blazorise.Components;
using EMQ.Client.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace EMQ.Client.Pages;

public partial class LibraryPage
{
    public enum LibrarySongFilterKind
    {
        All,
        MissingVideoOrSound,
        MissingBoth
    }

    public class AddSongLinkModel
    {
        [Required]
        [RegularExpression(RegexPatterns.SongLinkUrlRegex, ErrorMessage = "Invalid Url")]
        public string Url { get; set; } = "";
    }

    private AddSongLinkModel _addSongLinkModel = new();

    private ReviewQueueComponent? _reviewQueueComponent { get; set; }

    public string? selectedMusicSourceTitle { get; set; }

    public int? selectedArtistId { get; set; }

    public List<Song> CurrentSongs { get; set; } = new();

    public string NoSongsText { get; set; } = "";

    public bool ShowUploadCriteria { get; set; }

    public int ActiveSongId { get; set; }

    private string _selectedTab = "TabAutocompleteMst";

    public LibrarySongFilterKind LibrarySongFilter { get; set; }

    public int VisibleSongsCount { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
    }

    private Task OnSelectedTabChanged(string name)
    {
        _selectedTab = name;
        return Task.CompletedTask;
    }

    private async Task SelectedResultChangedMst()
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

                if (!CurrentSongs.Any())
                {
                    NoSongsText = "No results.";
                }

                StateHasChanged();
            }
        }
    }

    private async Task SelectedResultChangedA()
    {
        if (selectedArtistId is > 0)
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var res = await _client.PostAsJsonAsync("Library/FindSongsByArtistId", selectedArtistId);
            if (res.IsSuccessStatusCode)
            {
                List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                if (songs != null && songs.Any())
                {
                    CurrentSongs = songs;
                    selectedArtistId = null;
                }
            }

            if (!CurrentSongs.Any())
            {
                NoSongsText = "No results.";
            }

            StateHasChanged();
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

    private async Task OnclickButtonFetchMyList(MouseEventArgs arg)
    {
        var session = ClientState.Session;
        if (session?.VndbInfo.Labels != null)
        {
            var req = new ReqFindSongsByLabels(session.VndbInfo.Labels);
            var res = await _client.PostAsJsonAsync("Library/FindSongsByLabels", req);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadFromJsonAsync<List<Song>>();
                if (content is not null)
                {
                    CurrentSongs = content;
                }
            }
        }
        StateHasChanged();
    }

    private async Task OnLibrarySongFilterChanged(ChangeEventArgs arg)
    {
        LibrarySongFilter = Enum.Parse<LibrarySongFilterKind>((string)arg.Value!);
        // StateHasChanged();
    }
}
