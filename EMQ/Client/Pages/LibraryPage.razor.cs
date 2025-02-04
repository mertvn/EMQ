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
    private ReviewQueueComponent? _reviewQueueComponent { get; set; } // todo? remove

    public string? selectedMusicSourceTitle { get; set; }

    public AutocompleteA? selectedArtist { get; set; }

    public string? selectedMusicTitle { get; set; }

    public List<Song> CurrentSongs { get; set; } = new();

    public string NoSongsText { get; set; } = "";

    private string _selectedTab = "TabAutocompleteMst";

    private string _selectedTabVndb = "TabVNDB";

    private string _selectedTabStats = "TabAll";

    private string _selectedTabQueue { get; set; } = "TabReviewQueue";

    public LibrarySongFilterKind LibrarySongFilter { get; set; }

    public string VndbAdvsearchStr { get; set; } = "";

    public Tabs? TabsComponent { get; set; }

    public Tabs? TabsComponentVndb { get; set; }

    // https://github.com/dotnet/aspnetcore/issues/22159#issuecomment-635427175
    private TaskCompletionSource<bool>? _scheduledRenderTcs;

    // Only for Search by room settings tab
    private Room Room { get; } = new(Guid.Empty, "", new Player(-1, "", Avatar.DefaultAvatar));

    private bool IsMergeBGMTabs { get; set; }

    public SongSourceSongTypeMode SSSTMFilter { get; set; } = SongSourceSongTypeMode.Vocals;

    private QuizSettingsComponent? _quizSettingsComponent;

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

    private Task OnSelectedTabChangedVndb(string name)
    {
        _selectedTabVndb = name;
        return Task.CompletedTask;
    }

    private Task OnSelectedTabChangedStats(string name)
    {
        _selectedTabStats = name;
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
                await TabsComponentVndb!.SelectTab("TabVNDB");
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
            await TabsComponentVndb!.SelectTab("TabVNDB");
        }
    }

    public async Task SelectedResultChangedMt()
    {
        if (!string.IsNullOrWhiteSpace(selectedMusicTitle))
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var req = new ReqFindSongsBySongTitle(selectedMusicTitle);
            var res = await _client.PostAsJsonAsync("Library/FindSongsBySongTitle", req);
            if (res.IsSuccessStatusCode)
            {
                List<Song>? songs = await res.Content.ReadFromJsonAsync<List<Song>>().ConfigureAwait(false);
                if (songs != null && songs.Any())
                {
                    CurrentSongs = songs;
                    selectedMusicTitle = null;
                }
            }

            NoSongsText = "No results.";

            await StateHasChangedAsync();
            await TabsComponentVndb!.SelectTab("TabVNDB");
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
            await TabsComponentVndb!.SelectTab("TabVNDB");
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
        await TabsComponentVndb!.SelectTab("TabVNDB");
    }

    public async Task SelectedResultChangedDifficulty(SongDifficultyLevel difficulty, SongSourceSongTypeMode mode)
    {
        CurrentSongs = new List<Song>();
        NoSongsText = "Loading...";
        StateHasChanged();

        var res = await _client.PostAsJsonAsync("Library/FindSongsByDifficulty",
            new ReqFindSongsByDifficulty(difficulty, mode));
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
        await TabsComponentVndb!.SelectTab("TabVNDB");
    }

    public async Task SelectedResultChangedWarning(MediaAnalyserWarningKind warning, SongSourceSongTypeMode mode)
    {
        CurrentSongs = new List<Song>();
        NoSongsText = "Loading...";
        StateHasChanged();

        var res = await _client.PostAsJsonAsync("Library/FindSongsByWarnings",
            new ReqFindSongsByWarnings(new[] { warning }, mode));
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
        await TabsComponentVndb!.SelectTab("TabVNDB");
    }

    private async Task OnclickButtonFetchMyList(MouseEventArgs arg)
    {
        if (ClientState.VndbInfo.Labels != null)
        {
            CurrentSongs = new List<Song>();
            NoSongsText = "Loading...";
            StateHasChanged();

            var req = new ReqFindSongsByLabels(ClientState.VndbInfo.Labels, SSSTMFilter);
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
        await TabsComponentVndb!.SelectTab("TabVNDB");
    }

    private async Task OnLibrarySongFilterChanged(ChangeEventArgs arg)
    {
        LibrarySongFilter = Enum.Parse<LibrarySongFilterKind>((string)arg.Value!);

        // count doesn't update correctly unless we do this (???)
        await StateHasChangedAsync();
    }

    private async Task OnLibrarySongOrderChanged(ChangeEventArgs arg)
    {
        // Console.WriteLine("sorting");
        var orderKind = Enum.Parse<LibrarySongOrderKind>((string)arg.Value!);
        CurrentSongs = orderKind switch
        {
            LibrarySongOrderKind.Id => CurrentSongs.OrderBy(x => x.Id).ToList(),
            LibrarySongOrderKind.VoteAverage => CurrentSongs.OrderByDescending(x => x.VoteAverage).ToList(),
            LibrarySongOrderKind.PlayCount => CurrentSongs
                .OrderByDescending(x => x.Stats.GetValueOrDefault(GuessKind.Mst)?.TimesPlayed ?? 0).ToList(),
            LibrarySongOrderKind.GuessRate => CurrentSongs
                .OrderByDescending(x => x.Stats.GetValueOrDefault(GuessKind.Mst)?.CorrectPercentage ?? 0).ToList(),
            LibrarySongOrderKind.MyVote => CurrentSongs
                .OrderByDescending(x => ClientState.MusicVotes.GetValueOrDefault(x.Id)?.vote ?? 0).ToList(),
            _ => throw new ArgumentOutOfRangeException()
        };
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
            await TabsComponentVndb!.SelectTab("TabVNDB");
        }
    }

    private async Task Onclick_SearchByQuizSettings()
    {
        var res = await _client.PostAsJsonAsync("Library/FindSongsByQuizSettings",
            _quizSettingsComponent!.ClientQuizSettings);
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
            await TabsComponentVndb!.SelectTab("TabVNDB");
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

    [Description("Missing video link")]
    MissingVideo,

    [Description("Missing sound link")]
    MissingSound,

    [Description("Missing both links")]
    MissingBoth,

    [Description("Missing composer info")]
    MissingComposer,

    [Description("Missing arranger info")]
    MissingArranger,

    [Description("Missing lyricist info")]
    MissingLyricist,

    [Description("Missing ErogameScape music link")]
    MissingErogameScapeMusic,

    [Description("Voted")]
    Voted,

    [Description("Unvoted")]
    Unvoted,
}

public enum LibrarySongOrderKind
{
    [Description("Id")]
    Id,

    [Description("Vote average")]
    VoteAverage,

    [Description("Play count")]
    PlayCount,

    [Description("Guess rate")]
    GuessRate,

    [Description("My vote")]
    MyVote,
}
