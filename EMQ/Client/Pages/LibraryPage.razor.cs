using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise;
using EMQ.Client.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace EMQ.Client.Pages;

public partial class LibraryPage
{
    private ReviewQueueComponent? _reviewQueueComponent { get; set; }

    public string? selectedMusicSourceTitle { get; set; }

    public AutocompleteA? selectedArtist { get; set; }

    public List<Song> CurrentSongs { get; set; } = new();

    public string NoSongsText { get; set; } = "";

    private string _selectedTab = "TabAutocompleteMst";

    private string _selectedTab2 = "TabVNDB";

    public LibrarySongFilterKind LibrarySongFilter { get; set; }

    public string VndbAdvsearchStr { get; set; } = "";

    public Tabs? TabsComponent { get; set; }

    // https://github.com/dotnet/aspnetcore/issues/22159#issuecomment-635427175
    private TaskCompletionSource<bool>? _scheduledRenderTcs;

    private async Task StateHasChangedAsync()
    {
        if (_scheduledRenderTcs == null)
        {
            // No render is scheduled, so schedule one now
            var tcs = _scheduledRenderTcs = new TaskCompletionSource<bool>();
            await Task.Yield();
            StateHasChanged();
            _scheduledRenderTcs = null;
            tcs.SetResult(true);
        }
        else
        {
            // Just return the task corresponding to the existing scheduled render
            await _scheduledRenderTcs.Task;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
    }

    private Task OnSelectedTabChanged(string name)
    {
        _selectedTab = name;
        return Task.CompletedTask;
    }

    private Task OnSelectedTabChanged2(string name)
    {
        _selectedTab2 = name;
        return Task.CompletedTask;
    }

    public async Task SelectedResultChangedMst()
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
                    selectedMusicSourceTitle = null;
                }

                NoSongsText = "No results.";

                await StateHasChangedAsync();
                await TabsComponent!.SelectTab("TabVNDB");
            }
        }
    }

    public async Task SelectedResultChangedA()
    {
        if (selectedArtist?.AId is > 0)
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var res = await _client.PostAsJsonAsync("Library/FindSongsByArtistId", selectedArtist.AId);
            if (res.IsSuccessStatusCode)
            {
                List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                if (songs != null && songs.Any())
                {
                    CurrentSongs = songs;
                    selectedArtist = null;
                }
            }

            NoSongsText = "No results.";

            await StateHasChangedAsync();
            await TabsComponent!.SelectTab("TabVNDB");
        }
    }

    public async Task SelectedResultChangedUploader(string uploader)
    {
        if (!string.IsNullOrWhiteSpace(uploader))
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var res = await _client.PostAsJsonAsync("Library/FindSongsByUploader", uploader);
            if (res.IsSuccessStatusCode)
            {
                List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                if (songs != null && songs.Any())
                {
                    CurrentSongs = songs;
                }
            }

            NoSongsText = "No results.";

            await StateHasChangedAsync();
            await TabsComponent!.SelectTab("TabVNDB");
        }
    }

    public async Task SelectedResultChangedYear(DateTime year, SongSourceSongTypeMode mode)
    {
        CurrentSongs = new List<Song>();
        NoSongsText = "Loading...";
        StateHasChanged();

        var res = await _client.PostAsJsonAsync("Library/FindSongsByYear", new ReqFindSongsByYear(year.Year, mode));
        if (res.IsSuccessStatusCode)
        {
            List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
            if (songs != null && songs.Any())
            {
                CurrentSongs = songs;
            }
        }

        NoSongsText = "No results.";

        await StateHasChangedAsync();
        await TabsComponent!.SelectTab("TabVNDB");
    }

    private async Task OnclickButtonFetchMyList(MouseEventArgs arg)
    {
        if (ClientState.VndbInfo.Labels != null)
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var req = new ReqFindSongsByLabels(ClientState.VndbInfo.Labels);
            var res = await _client.PostAsJsonAsync("Library/FindSongsByLabels", req);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadFromJsonAsync<List<Song>>();
                if (content is not null)
                {
                    CurrentSongs = content;
                }
            }

            NoSongsText = "No results.";
        }

        await StateHasChangedAsync();
        await TabsComponent!.SelectTab("TabVNDB");
    }

    private async void OnLibrarySongFilterChanged(ChangeEventArgs arg)
    {
        LibrarySongFilter = Enum.Parse<LibrarySongFilterKind>((string)arg.Value!);

        // count doesn't update correctly unless we do this (???)
        await StateHasChangedAsync();
    }

    private async Task OnclickButtonFetchByVndbAdvsearchStr(MouseEventArgs arg)
    {
        VndbAdvsearchStr = VndbAdvsearchStr.SanitizeVndbAdvsearchStr();
        if (!string.IsNullOrWhiteSpace(VndbAdvsearchStr))
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            string[]? vndbUrls =
                await VndbMethods.GetVnUrlsMatchingAdvsearchStr(ClientState.VndbInfo, VndbAdvsearchStr);
            if (vndbUrls != null && vndbUrls.Any())
            {
                var req = vndbUrls;
                var res = await _client.PostAsJsonAsync("Library/FindSongsByVndbAdvsearchStr", req);
                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadFromJsonAsync<List<Song>>();
                    if (content is not null)
                    {
                        CurrentSongs = content;
                    }
                }
            }

            NoSongsText = "No results.";

            await StateHasChangedAsync();
            await TabsComponent!.SelectTab("TabVNDB");
        }
    }
}

public enum LibrarySongFilterKind
{
    [Description("All")]
    All,

    [Description("Missing only video link")]
    MissingOnlyVideo,

    [Description("Missing only sound link")]
    MissingOnlySound,

    [Description("Missing video or sound link")]
    MissingVideoOrSound,

    [Description("Missing both links")]
    MissingBoth
}
