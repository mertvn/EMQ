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

    private string _selectedTab { get; set; } = "TabAutocompleteMst";

    private string _selectedTabVndb { get; set; } = "TabVNDB";

    private string _selectedTabStats { get; set; } = "TabAll";

    private string _selectedTabQueue { get; set; } = "TabReviewQueue";

    public string VndbAdvsearchStr { get; set; } = "";

    public Tabs? TabsComponent { get; set; }

    public Tabs? TabsComponentVndb { get; set; }

    // https://github.com/dotnet/aspnetcore/issues/22159#issuecomment-635427175
    private TaskCompletionSource<bool>? _scheduledRenderTcs;

    // Only for Search by room settings tab
    private Room Room { get; } = new(Guid.Empty, "", new Player(-1, "", Avatar.DefaultAvatar));

    private bool IsMergeBGMTabs { get; set; } = true;

    public SongSourceSongTypeMode SSSTMFilter { get; set; } = SongSourceSongTypeMode.Vocals;

    private QuizSettingsComponent? _quizSettingsComponent;

    [SupplyParameterFromQuery(Name = "mId")]
    private int QueryMId { get; set; }

    public SongFilterComponent songFilterComponentRef { get; set; } = null!;

    private string[] ServerUploadQueue { get; set; } = Array.Empty<string>();

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
        if (QueryMId > 0)
        {
            selectedMusicTitle = QueryMId.ToString();
            await SelectedResultChangedMt();
        }
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

    // todo reapply this after searching
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
            LibrarySongOrderKind.SSST => CurrentSongs.OrderBy(x => x.Sources.First().SongTypes.First()).ToList(),
            LibrarySongOrderKind.CommentCount => CurrentSongs.OrderByDescending(x => x.CommentCount).ToList(),
            LibrarySongOrderKind.CollectionCount => CurrentSongs.OrderByDescending(x => x.CollectionCount).ToList(),
            LibrarySongOrderKind.MyVNDBVote => CurrentSongs
                .OrderByDescending(x =>
                {
                    int vote = 0;
                    if (ClientState.VndbInfo.Labels != null)
                    {
                        string[] vndbUrls = x.Sources
                            .Select(y => y.Links.FirstOrDefault(z => z.Type == SongSourceLinkType.VNDB))
                            .Select(y => y?.Url ?? "").ToArray();
                        foreach (var label in ClientState.VndbInfo.Labels.TakeWhile(_ => vote <= 0))
                        {
                            foreach ((string? key, int value) in label.VNs)
                            {
                                if (vndbUrls.Contains(key))
                                {
                                    vote = value;
                                    break;
                                }
                            }
                        }
                    }

                    return vote;
                }).ToList(),
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

    private async Task GetUploadQueue()
    {
        ServerUploadQueue = (await _client.GetFromJsonAsync<string[]>("Library/GetUploadQueue"))!;
    }
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

    [Description("Song source song type")]
    SSST,

    [Description("Comment count")]
    CommentCount,

    [Description("My VNDB vote")]
    MyVNDBVote,

    [Description("Collection count")]
    CollectionCount,
}
