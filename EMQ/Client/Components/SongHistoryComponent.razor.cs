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
                    PlayerStatsComponent.CalculatePlayerStats(SongsHistory.Select(x => x.Value).ToArray());
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
        string json = JsonSerializer.Serialize(SongsHistory, Utils.JsoIndented);
        byte[] file = System.Text.Encoding.UTF8.GetBytes(json);
        await _jsRuntime.InvokeVoidAsync("downloadFile", $"EMQ_SongHistory_{DateTime.UtcNow:yyyy-MM-ddTHH_mm_ss}.json",
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
}
