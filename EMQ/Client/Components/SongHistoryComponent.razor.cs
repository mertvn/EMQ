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

    protected override async Task OnParametersSetAsync()
    {
        // Console.WriteLine("paramset songhistory");
        if (SongsHistory != null)
        {
            int newCount = SongsHistory.Count(x => x.Value.PlayerGuessInfos.Any());
            if (newCount != _previousCount)
            {
                PlayerStatsDict = CalculatePlayerStats(SongsHistory);
            }

            _previousCount = newCount;
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

    public static Dictionary<int, PlayerStats> CalculatePlayerStats(Dictionary<int, SongHistory> songsHistory)
    {
        Console.WriteLine("CalculatePlayerStats");
        var dict = new Dictionary<int, PlayerStats>();
        var songHistories = songsHistory.Select(x => x.Value).ToArray();
        int[] playerIds = songsHistory.SelectMany(x => x.Value.PlayerGuessInfos.Select(y => y.Key)).Distinct()
            .OrderBy(x => x).ToArray();
        foreach (int playerId in playerIds)
        {
            var pgis = songHistories.SelectMany(x => x.PlayerGuessInfos).Where(y => y.Key == playerId)
                .Select(z => (z.Value)).ToArray();

            string username = pgis.First().Username;
            const int quizCount = 1; // todo?
            HashSet<int> songIds = new();

            int timesCorrect = 0;
            int timesPlayed = pgis.Length;
            int timesGuessed = 0;
            int totalGuessMs = 0;

            float totalDiff = 0;
            float totalHits = 0;

            int opCount = 0;
            int edCount = 0;
            int insCount = 0;
            int bgmCount = 0;

            int opHit = 0;
            int edHit = 0;
            int insHit = 0;
            int bgmHit = 0;

            int rigOpCount = 0;
            int rigEdCount = 0;
            int rigInsCount = 0;
            int rigBgmCount = 0;

            int erigs = 0;
            int rigs = 0;
            int rigsHit = 0;
            int offlistHit = 0;
            int offlistCount = 0;

            foreach (SongHistory songHistory in songHistories)
            {
                foreach ((int key, GuessInfo value) in songHistory.PlayerGuessInfos)
                {
                    if (key == playerId)
                    {
                        songIds.Add(songHistory.Song.Id);
                        if (!string.IsNullOrWhiteSpace(value.Guess))
                        {
                            timesGuessed += 1;
                            totalGuessMs += value.FirstGuessMs;
                        }

                        // todo? cache this
                        var songSourceSongTypes = songHistory.Song.Sources.SelectMany(x => x.SongTypes).ToArray();
                        if (songSourceSongTypes.Contains(SongSourceSongType.OP))
                        {
                            opCount += 1;
                        }

                        if (songSourceSongTypes.Contains(SongSourceSongType.ED))
                        {
                            edCount += 1;
                        }

                        if (songSourceSongTypes.Contains(SongSourceSongType.Insert))
                        {
                            insCount += 1;
                        }

                        if (songSourceSongTypes.Contains(SongSourceSongType.BGM))
                        {
                            bgmCount += 1;
                        }

                        if (value.IsOnList)
                        {
                            rigs += 1;

                            if (songSourceSongTypes.Contains(SongSourceSongType.OP))
                            {
                                rigOpCount += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.ED))
                            {
                                rigEdCount += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.Insert))
                            {
                                rigInsCount += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.BGM))
                            {
                                rigBgmCount += 1;
                            }
                        }
                        else
                        {
                            offlistCount += 1;
                        }

                        if (value.IsGuessCorrect)
                        {
                            timesCorrect += 1;

                            totalDiff += songHistory.Song.Stats.CorrectPercentage;
                            totalHits += songHistory.TimesCorrect;

                            if (songHistory.TimesCorrect == 1)
                            {
                                erigs += 1;
                            }

                            if (value.IsOnList)
                            {
                                rigsHit += 1;
                            }
                            else
                            {
                                offlistHit += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.OP))
                            {
                                opHit += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.ED))
                            {
                                edHit += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.Insert))
                            {
                                insHit += 1;
                            }

                            if (songSourceSongTypes.Contains(SongSourceSongType.BGM))
                            {
                                bgmHit += 1;
                            }
                        }
                    }
                }
            }

            int uniqueSongs = songIds.Count;
            float avgDiff = totalDiff.Div0(timesCorrect);
            float avgOf8 = totalHits.Div0(timesCorrect);

            dict[playerId] = new PlayerStats
            {
                Username = username,
                QuizCount = quizCount,
                TimesCorrect = timesCorrect,
                TimesPlayed = timesPlayed,
                TimesGuessed = timesGuessed,
                TotalGuessMs = totalGuessMs,
                AvgDiff = avgDiff,
                Erigs = erigs,
                AvgOf8 = avgOf8,
                OpCount = opCount,
                EdCount = edCount,
                InsCount = insCount,
                BgmCount = bgmCount,
                OpHit = opHit,
                EdHit = edHit,
                InsHit = insHit,
                BgmHit = bgmHit,
                RigsHit = rigsHit,
                Rigs = rigs,
                RigOp = rigOpCount,
                RigEd = rigEdCount,
                RigIns = rigInsCount,
                RigBgm = rigBgmCount,
                UniqueSongs = uniqueSongs,
                OfflistHit = offlistHit,
                OfflistCount = offlistCount,
            };
        }

        return dict;
    }
}
