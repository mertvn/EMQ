using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.Components;
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
    private Dictionary<int, AddSongLinkModel> _addSongLinkModel = new();

    private ReviewQueueComponent? _reviewQueueComponent { get; set; }

    public string? selectedMusicSourceTitle { get; set; }

    public AutocompleteA? selectedArtist { get; set; }

    public List<Song> CurrentSongs { get; set; } = new();

    public string NoSongsText { get; set; } = "";

    public bool ShowUploadCriteria { get; set; }

    private string _selectedTab = "TabAutocompleteMst";

    public LibrarySongFilterKind LibrarySongFilter { get; set; }

    public int VisibleSongsCount { get; set; }

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

                if (!CurrentSongs.Any())
                {
                    NoSongsText = "No results.";
                }

                StateHasChanged();
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

        _addSongLinkModel[mId].Url = "";
        StateHasChanged();

        url = url.Trim().ToLowerInvariant();
        bool isVideo = url.IsVideoLink();
        SongLinkType songLinkType = url.Contains("catbox") ? SongLinkType.Catbox : SongLinkType.Unknown;

        string submittedBy = ClientState.Session.Player.Username;
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

    private async Task OnclickButtonFetchMyList(MouseEventArgs arg)
    {
        var session = ClientState.Session;
        if (session?.VndbInfo.Labels != null)
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

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

            if (!CurrentSongs.Any())
            {
                NoSongsText = "No results.";
            }
        }

        StateHasChanged();
    }

    private async void OnLibrarySongFilterChanged(ChangeEventArgs arg)
    {
        LibrarySongFilter = Enum.Parse<LibrarySongFilterKind>((string)arg.Value!);

        // count doesn't update correctly unless we do this (???)
        await StateHasChangedAsync();
    }

    private async Task OnclickButtonFetchByVndbAdvsearchStr(MouseEventArgs arg)
    {
        if (!string.IsNullOrWhiteSpace(VndbAdvsearchStr))
        {
            VndbAdvsearchStr = VndbAdvsearchStr.SanitizeVndbAdvsearchStr();

            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            string[]? vndbUrls =
                await VndbMethods.GetVnUrlsMatchingAdvsearchStr(ClientState.Session?.VndbInfo, VndbAdvsearchStr);
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

            if (!CurrentSongs.Any())
            {
                NoSongsText = "No results.";
            }

            StateHasChanged();
        }
    }
}

public enum LibrarySongFilterKind
{
    [Description("All")]
    All,

    [Description("Missing video or sound link")]
    MissingVideoOrSound,

    [Description("Missing both links")]
    MissingBoth
}

public class AddSongLinkModel
{
    [Required]
    [RegularExpression(RegexPatterns.SongLinkUrlRegex, ErrorMessage = "Invalid Url")]
    public string Url { get; set; } = "";
}
