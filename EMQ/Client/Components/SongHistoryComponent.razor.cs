using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class SongHistoryComponent
{
    [Parameter]
    public Dictionary<int, SongHistory>? SongsHistory { get; set; }

    public Dictionary<int, bool> RowDetailsDict { get; set; } = new();

    public Dictionary<int, PlayerStats> PlayerStatsDict { get; set; } = new();

    private SongHistoryFilterKind SongHistoryFilter { get; set; }

    private int _previousCount;

    private string SelectedPlayerUsername { get; set; } = "-";

    private GuessKind SelectedGuessKind { get; set; }

    private bool SessionWasNull { get; set; } = true;

    protected override async Task OnParametersSetAsync()
    {
        // Console.WriteLine("paramset songhistory");
        if (SongsHistory != null)
        {
            int newCount = SongsHistory.Count(x => x.Value.PlayerGuessInfos.Any());
            if (newCount != _previousCount)
            {
                PlayerStatsDict =
                    PlayerStatsComponent.CalculatePlayerStats(SongsHistory.Select(x => x.Value).ToArray(),
                        SelectedGuessKind);
            }

            _previousCount = newCount;
        }

        // meh, also doesn't really work for hotjoining players, meeeh
        if (SessionWasNull && ClientState.Session != null)
        {
            SessionWasNull = false;
            SelectedPlayerUsername = ClientState.Session.Player.Username;
        }
    }

    public async Task CallStateHasChanged()
    {
        await Task.Yield();
        StateHasChanged();
    }

    private async Task Onclick_SongHistoryRow(SongHistory songHistory)
    {
        if (RowDetailsDict.TryGetValue(songHistory.Song.Id, out bool showRowDetail))
        {
            RowDetailsDict[songHistory.Song.Id] = !showRowDetail;
        }
        else
        {
            RowDetailsDict[songHistory.Song.Id] = true;
        }

        await CallStateHasChanged();
    }

    private async Task DownloadSongHistoryJson()
    {
        var clone = JsonSerializer.Deserialize<Dictionary<int, SongHistory>?>(JsonSerializer.Serialize(SongsHistory));
        if (clone == null)
        {
            return;
        }

        var enabledGuessTypes = new HashSet<GuessKind>();
        foreach ((int _, SongHistory value) in clone)
        {
            value.Song.Sort();
            foreach (SongLink link in value.Song.Links)
            {
                link.AnalysisRaw = null;
                link.LastUnhandledReport = null;
                link.VocalsRanges = Array.Empty<TimeRange>();
            }

            // foreach (SongArtist songArtist in value.Song.Artists)
            // {
            //     songArtist.Links = songArtist.Links.Take(2).ToList();
            // }

            // foreach (SongSource songSource in value.Song.Sources)
            // {
            //     var mainTitles = songSource.Titles.Where(x => x.IsMainTitle).ToList();
            //     if (mainTitles.Any())
            //     {
            //         songSource.Titles = mainTitles;
            //     }
            // }

            foreach ((int _, Dictionary<GuessKind, GuessInfo> pgis) in value.PlayerGuessInfos)
            {
                foreach ((GuessKind guessKind, GuessInfo guessInfo) in pgis)
                {
                    enabledGuessTypes.Add(guessKind);
                    pgis[guessKind] = guessInfo with
                    {
                        CurrentUserSpacedRepetition = null, PreviousUserSpacedRepetition = null,
                    };
                }
            }
        }

        foreach ((int _, SongHistory value) in clone)
        {
            value.Song.Stats = value.Song.Stats.Where(x => enabledGuessTypes.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        string json = JsonSerializer.Serialize(clone, Utils.JsoIndentedNotDefault);
        byte[] file = System.Text.Encoding.UTF8.GetBytes(json);
        await _jsRuntime.InvokeVoidAsync("downloadFile",
            $"EMQ_SongHistory_{DateTime.UtcNow:yyyy-MM-ddTHH_mm_ss}.json",
            "application/json", file);
    }

    public enum SongHistoryFilterKind
    {
        All,
        Correct,
        Wrong,
        Rig,
        Erig,
    }

    private async Task OnSongHistoryFilterChanged(ChangeEventArgs arg)
    {
        SongHistoryFilter = Enum.Parse<SongHistoryFilterKind>((string)arg.Value!);
        await CallStateHasChanged();
    }

    private async Task OnSelectedPlayerUsernameChanged(string value)
    {
        SelectedPlayerUsername = value;
        await CallStateHasChanged();
    }

    private async Task OnSelectedGuessKindChanged(GuessKind value)
    {
        SelectedGuessKind = value;
        if (SongsHistory != null)
        {
            PlayerStatsDict =
                PlayerStatsComponent.CalculatePlayerStats(SongsHistory.Select(x => x.Value).ToArray(),
                    SelectedGuessKind);
        }

        await CallStateHasChanged();
    }
}
