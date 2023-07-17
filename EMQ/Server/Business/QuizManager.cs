using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Server.Db;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Hubs;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.VNDB.Business;
using Microsoft.AspNetCore.SignalR;

namespace EMQ.Server.Business;

public class QuizManager
{
    public QuizManager(Quiz quiz, IHubContext<QuizHub> hubContext)
    {
        Quiz = quiz;
        HubContext = hubContext;
    }

    public Quiz Quiz { get; }

    private IHubContext<QuizHub> HubContext { get; }

    private Dictionary<int, List<string>> CorrectAnswersDict { get; set; } = new();

    private Dictionary<int, SongStats> SongStatsDict { get; set; } = new();

    private void SetTimer()
    {
        if (!Quiz.IsDisposed)
        {
            Quiz.Timer.Stop();
            Quiz.Timer.Elapsed -= OnTimedEvent;

            Quiz.Timer.Interval = TimeSpan.FromMilliseconds(Quiz.TickRate).TotalMilliseconds;
            Quiz.Timer.Elapsed += OnTimedEvent;
            Quiz.Timer.AutoReset = true;
            Quiz.Timer.Start();
        }
    }

    private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            if (Quiz.QuizState.RemainingMs >= 0)
            {
                Quiz.QuizState.RemainingMs -= Quiz.TickRate;
            }

            if (Quiz.QuizState.RemainingMs <= 0)
            {
                if (!Quiz.IsDisposed)
                {
                    Quiz.Timer.Stop();
                }

                switch (Quiz.QuizState.Phase)
                {
                    case QuizPhaseKind.Guess:
                        await EnterJudgementPhase();
                        break;
                    case QuizPhaseKind.Judgement:
                        await EnterResultsPhase();
                        break;
                    case QuizPhaseKind.Results:
                        await EnterGuessingPhase();
                        break;
                    case QuizPhaseKind.Looting:
                        bool lootingSuccess = await SetLootedSongs();
                        await EnterQuiz();

                        if (!lootingSuccess)
                        {
                            Quiz.Room.Log("Canceling quiz due to looting failure", writeToChat: true);
                            await CancelQuiz();
                        }

                        await EnterGuessingPhase();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!Quiz.IsDisposed)
                {
                    Quiz.Timer.Start();
                }
            }
        }
    }

    public async Task CancelQuiz()
    {
        Quiz.QuizState.QuizStatus = QuizStatus.Canceled;

        if (!Quiz.IsDisposed)
        {
            Quiz.Timer.Stop();
            Quiz.Timer.Elapsed -= OnTimedEvent;
        }

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values).SendAsync("ReceiveQuizCanceled");
    }

    private async Task EnterGuessingPhase()
    {
        while (Quiz.QuizState.IsPaused)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        int timeoutMs = Quiz.Room.QuizSettings.TimeoutMs;
        // Console.WriteLine("ibc " + isBufferedCount);

        int activePlayersCount = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
            .Count(x => x.Player.HasActiveConnection);
        // Room.Log($"activePlayers: {activePlayers}/{Quiz.Room.Players.Count}");

        float waitNumber = (float)Math.Round(
            activePlayersCount * ((float)Quiz.Room.QuizSettings.WaitPercentage / 100),
            MidpointRounding.AwayFromZero);

        while (isBufferedCount < waitNumber &&
               timeoutMs > 0)
        {
            // Console.WriteLine("in while " + isBufferedCount + "/" + waitNumber);
            await Task.Delay(1000);
            timeoutMs -= 1000;

            isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);

            activePlayersCount = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
                .Count(x => x.Player.HasActiveConnection);

            waitNumber = (float)Math.Round(
                activePlayersCount * ((float)Quiz.Room.QuizSettings.WaitPercentage / 100),
                MidpointRounding.AwayFromZero);

            Quiz.QuizState.ExtraInfo =
                $"Waiting buffering... {isBufferedCount}/{waitNumber} timeout in {timeoutMs / 1000}s";
            // Console.WriteLine("ei: " + Quiz.QuizState.ExtraInfo);

            await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
        }

        Quiz.QuizState.Phase = QuizPhaseKind.Guess;
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.GuessMs;
        Quiz.QuizState.sp += 1;
        Quiz.QuizState.ExtraInfo = "";

        foreach (var player in Quiz.Room.Players)
        {
            player.Guess = "";
            player.FirstGuessMs = 0;
            player.PlayerStatus = PlayerStatus.Thinking;
            player.IsBuffered = false;
            player.IsSkipping = false;
            player.IsReadiedUp = false;
        }

        // reset the guesses
        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceivePlayerGuesses", Quiz.Room.PlayerGuesses);

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceiveUpdateRoom", Quiz.Room, true);
    }

    private async Task EnterJudgementPhase()
    {
        Quiz.QuizState.Phase = QuizPhaseKind.Judgement;
        Quiz.QuizState.ExtraInfo = "";

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceiveUpdateRoom", Quiz.Room, true);

        if (Quiz.Room.QuizSettings.TeamSize > 1)
        {
            await DetermineTeamGuesses();
        }

        // need to do this AFTER the team guesses have been determined
        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceivePlayerGuesses", Quiz.Room.PlayerGuesses);

        await JudgeGuesses();
    }

    private async Task DetermineTeamGuesses()
    {
        List<int> processedTeamIds = new();
        foreach (Player player in Quiz.Room.Players)
        {
            if (processedTeamIds.Contains(player.TeamId))
            {
                continue;
            }

            if (IsGuessCorrect(player.Guess))
            {
                processedTeamIds.Add(player.TeamId);
                foreach (Player teammate in Quiz.Room.Players.Where(teammate => teammate.TeamId == player.TeamId))
                {
                    teammate.Guess = player.Guess;
                }
            }
        }

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
    }

    private bool IsGuessCorrect(string guess)
    {
        if (!CorrectAnswersDict.TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
        {
            if (Quiz.QuizState.sp < 0 || Quiz.QuizState.sp > Quiz.Songs.Count)
            {
                throw new Exception($"Invalid quiz state sp: {Quiz.QuizState.sp} SongsCount: {Quiz.Songs.Count}");
            }

            correctAnswers = Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
                .Select(x => x.LatinTitle).ToList();
            correctAnswers.AddRange(Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
                .Select(x => x.NonLatinTitle).Where(x => x != null)!);
            correctAnswers = correctAnswers.Distinct().ToList();

            CorrectAnswersDict.Add(Quiz.QuizState.sp, correctAnswers);

            // Console.WriteLine("-------");
            Quiz.Room.Log("cA: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
        }

        bool correct = correctAnswers.Any(correctAnswer =>
            string.Equals(guess, correctAnswer, StringComparison.OrdinalIgnoreCase));

        return correct;
    }

    private async Task EnterResultsPhase()
    {
        Quiz.QuizState.Phase = QuizPhaseKind.Results;
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.ResultsMs;

        foreach (var player in Quiz.Room.Players)
        {
            player.IsBuffered = false;
        }

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceiveCorrectAnswer", Quiz.Songs[Quiz.QuizState.sp],
                Quiz.Songs[Quiz.QuizState.sp].PlayerLabels,
                Quiz.Room.PlayerGuesses);

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
            .SendAsync("ReceiveUpdateRoom", Quiz.Room, true);

        if (Quiz.QuizState.sp + 1 == Quiz.Songs.Count ||
            (Quiz.Room.QuizSettings.MaxLives > 0 && !Quiz.Room.Players.Any(x => x.Lives > 0)))
        {
            await EndQuiz();
        }

        if (Quiz.Room.HotjoinQueue.Any())
        {
            while (Quiz.Room.HotjoinQueue.Any())
            {
                Quiz.Room.HotjoinQueue.TryDequeue(out Player? player);
                if (player != null)
                {
                    Quiz.Room.Players.Enqueue(player);
                    Quiz.Room.RemoveSpectator(player);
                    Quiz.Room.Log($"{player.Username} hotjoined.", player.Id, true);
                }
            }

            await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
        }
    }

    private async Task JudgeGuesses()
    {
        await Task.Delay(TimeSpan.FromSeconds(2)); // add suspense & wait for late guesses

        var songStats = new SongStats();
        int numCorrect = 0;
        int numActivePlayers = 0;
        int numGuesses = 0;
        int totalGuessMs = 0;

        foreach (var player in Quiz.Room.Players)
        {
            if (player.PlayerStatus == PlayerStatus.Dead)
            {
                continue;
            }

            Quiz.Room.Log("pG: " + player.Guess, player.Id);

            bool correct = IsGuessCorrect(player.Guess);
            if (correct)
            {
                player.Score += 1;
                player.PlayerStatus = PlayerStatus.Correct;
            }
            else
            {
                player.PlayerStatus = PlayerStatus.Wrong;

                if (Quiz.Room.QuizSettings.MaxLives > 0 && player.Lives >= 0)
                {
                    player.Lives -= 1;
                    if (player.Lives <= 0)
                    {
                        player.PlayerStatus = PlayerStatus.Dead;
                    }
                }
            }

            if (player.HasActiveConnection)
            {
                numActivePlayers += 1;

                if (!string.IsNullOrWhiteSpace(player.Guess))
                {
                    numGuesses += 1;
                    totalGuessMs += player.FirstGuessMs;
                }

                if (correct)
                {
                    numCorrect += 1;
                }
            }
        }

        songStats.TimesCorrect = numCorrect;
        songStats.TimesPlayed = numActivePlayers;
        songStats.TimesGuessed = numGuesses;
        songStats.TotalGuessMs = totalGuessMs;
        SongStatsDict.Add(Quiz.Songs[Quiz.QuizState.sp].Id, songStats);
    }

    public async Task EndQuiz()
    {
        Quiz.Room.Log("Ended");
        Quiz.QuizState.QuizStatus = QuizStatus.Ended;

        if (!Quiz.IsDisposed)
        {
            Quiz.Timer.Stop();
            Quiz.Timer.Elapsed -= OnTimedEvent;
        }

        Quiz.QuizState.ExtraInfo = "Quiz ended. Returning to room...";

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values).SendAsync("ReceiveQuizEnded");

        Directory.CreateDirectory("RoomLog");
        await File.WriteAllTextAsync($"RoomLog/r{Quiz.Room.Id}q{Quiz.Id}.json",
            JsonSerializer.Serialize(Quiz.Room.RoomLog, Utils.JsoIndented));

        bool shouldUpdateStats = Quiz.Room.QuizSettings.SongSelectionKind == SongSelectionKind.Random &&
                                 !Quiz.Room.QuizSettings.Filters.CategoryFilters.Any() &&
                                 !Quiz.Room.QuizSettings.Filters.ArtistFilters.Any() &&
                                 !Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter.Any();
        if (shouldUpdateStats)
        {
            await UpdateStats(SongStatsDict);
        }
        else
        {
            Quiz.Room.Log("Not updating stats");
        }
    }

    private static async Task UpdateStats(Dictionary<int, SongStats> songStatsDict)
    {
        foreach ((int mId, SongStats songStats) in songStatsDict)
        {
            if (songStats.TimesPlayed > 0)
            {
                await DbManager.IncrementSongStats(mId, songStats, null);
            }
        }
    }

    private async Task EnterQuiz()
    {
        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values).SendAsync("ReceiveQuizEntered");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async Task<bool> PrimeQuiz()
    {
        CorrectAnswersDict = new Dictionary<int, List<string>>();
        List<string> validSources = new();

        foreach (Player player in Quiz.Room.Players)
        {
            player.Lives = Quiz.Room.QuizSettings.MaxLives;
            player.Score = 0;
            player.Guess = "";
            player.FirstGuessMs = 0;
            player.IsBuffered = false;
            player.IsSkipping = false;
            // do not set player.IsReadiedUp to false here, because it would be annoying to ready up again if we return false
            player.PlayerStatus = PlayerStatus.Default;
            player.LootingInfo = new PlayerLootingInfo();

            if (Quiz.Room.QuizSettings.OnlyFromLists)
            {
                var session = ServerState.Sessions.Single(x => x.Player.Id == player.Id);
                if (session.VndbInfo.Labels != null)
                {
                    validSources.AddRange(Label.GetValidSourcesFromLabels(session.VndbInfo.Labels));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter))
        {
            Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter =
                Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter.SanitizeVndbAdvsearchStr();

            string[]? vndbUrls =
                await VndbMethods.GetVnUrlsMatchingAdvsearchStr(null,
                    Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter);

            if (vndbUrls == null || !vndbUrls.Any())
            {
                Quiz.Room.Log($"VNDB search filter returned no results.", -1, true);
                return false;
            }

            validSources = vndbUrls.Distinct().ToList();
            Quiz.Room.Log($"VNDB search filter returned {validSources.Count} results.", -1, true);
            Quiz.Room.Log("validSources overridden by VndbAdvsearchFilter: " +
                          JsonSerializer.Serialize(validSources, Utils.Jso));
        }
        else
        {
            validSources = validSources.Distinct().ToList();
            Quiz.Room.Log("validSources: " + JsonSerializer.Serialize(validSources, Utils.Jso));
        }

        Quiz.Room.Log($"validSourcesCount: {validSources.Count}");

        var validCategories = Quiz.Room.QuizSettings.Filters.CategoryFilters;
        Quiz.Room.Log("validCategories: " + JsonSerializer.Serialize(validCategories, Utils.Jso));
        Quiz.Room.Log($"validCategoriesCount: {validCategories.Count}");

        var validArtists = Quiz.Room.QuizSettings.Filters.ArtistFilters;
        Quiz.Room.Log("validArtists: " + JsonSerializer.Serialize(validArtists, Utils.Jso));
        Quiz.Room.Log($"validArtistsCount: {validArtists.Count}");

        List<Song> dbSongs;
        switch (Quiz.Room.QuizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
                dbSongs = await DbManager.GetRandomSongs(Quiz.Room.QuizSettings.NumSongs,
                    Quiz.Room.QuizSettings.Duplicates, validSources,
                    filters: Quiz.Room.QuizSettings.Filters);

                if (dbSongs.Count == 0)
                {
                    return false;
                }

                foreach (Song dbSong in dbSongs)
                {
                    dbSong.PlayerLabels = GetPlayerLabelsForSong(dbSong);
                }

                Quiz.Songs = dbSongs;
                Quiz.QuizState.NumSongs = Quiz.Songs.Count;
                break;
            case SongSelectionKind.Looting:
                dbSongs = await DbManager.GetRandomSongs(
                    Quiz.Room.QuizSettings.NumSongs * ((Quiz.Room.Players.Count + 4) / 2),
                    Quiz.Room.QuizSettings.Duplicates, validSources,
                    filters: Quiz.Room.QuizSettings.Filters);

                if (dbSongs.Count == 0)
                {
                    return false;
                }

                var validSourcesLooting = new Dictionary<string, List<Title>>();
                foreach (Song dbSong in dbSongs)
                {
                    foreach (var dbSongSource in dbSong.Sources)
                    {
                        // todo songs with multiple vns overriding each other
                        validSourcesLooting[dbSongSource.Links.First(x => x.Type == SongSourceLinkType.VNDB).Url] =
                            dbSongSource.Titles;
                    }
                }

                Quiz.ValidSourcesForLooting = validSourcesLooting;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        foreach (Song dbSong in dbSongs)
        {
            dbSong.Links = SongLink.FilterSongLinks(dbSong.Links);
        }

        // Console.WriteLine(JsonSerializer.Serialize(Quiz.Songs));
        Quiz.QuizState.ExtraInfo = "Waiting buffering...";

        return true;
    }

    public async Task StartQuiz()
    {
        Quiz.QuizState.QuizStatus = QuizStatus.Playing;

        // await EnterQuiz();
        // await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds.Values).SendAsync("ReceiveQuizStarted");

        switch (Quiz.Room.QuizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
                await EnterQuiz();
                await EnterGuessingPhase();
                await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                    .SendAsync("ReceiveQuizStarted");
                break;
            case SongSelectionKind.Looting:
                await EnterLootingPhase();
                await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                    .SendAsync("ReceivePyramidEntered");
                await Task.Delay(TimeSpan.FromSeconds(1));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        SetTimer();
    }

    public async Task OnSendPlayerIsBuffered(int playerId, string source)
    {
        Player? player = Quiz.Room.Players.SingleOrDefault(player => player.Id == playerId);
        if (player == null)
        {
            // early return if spectator
            return;
        }

        player.IsBuffered = true;
        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        Quiz.Room.Log($"isBufferedCount: {isBufferedCount} Source: {source}", playerId);
    }

    public async Task OnSendPlayerJoinedQuiz(string connectionId, int playerId)
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            await HubContext.Clients.Clients(connectionId).SendAsync("ReceiveQuizStarted");

            // todo player initialization logic shouldn't be here at all after the user + player separation
            var player = Quiz.Room.Players.SingleOrDefault(x => x.Id == playerId);
            if (player == null)
            {
                // early return if spectator
                return;
            }

            if (player.Score > 0 || player.Guess != "" || (Quiz.Room.QuizSettings.MaxLives > 0 &&
                                                           player.Lives != Quiz.Room.QuizSettings.MaxLives))
            {
                return;
            }

            player.Lives = Quiz.Room.QuizSettings.MaxLives;
            player.Score = 0;
            player.Guess = "";
            player.FirstGuessMs = 0;
            player.IsBuffered = false;
            player.IsSkipping = false;
            player.IsReadiedUp = false;
            player.PlayerStatus = PlayerStatus.Default;

            if (Quiz.Room.QuizSettings.TeamSize > 1)
            {
                var teammate = Quiz.Room.Players.First(x => x.TeamId == player.TeamId);
                player.Lives = teammate.Lives;
                player.Score = teammate.Score;
            }

            await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
        }
    }

    public async Task OnSendGuessChanged(string connectionId, int playerId, string guess)
    {
        if (Quiz.QuizState.Phase is QuizPhaseKind.Guess or QuizPhaseKind.Judgement)
        {
            var player = Quiz.Room.Players.SingleOrDefault(x => x.Id == playerId);
            if (player != null)
            {
                player.Guess = guess;
                player.PlayerStatus = PlayerStatus.Guessed;

                if (player.FirstGuessMs <= 0)
                {
                    switch (Quiz.QuizState.Phase)
                    {
                        case QuizPhaseKind.Guess:
                            player.FirstGuessMs = Quiz.Room.QuizSettings.GuessMs - (int)Quiz.QuizState.RemainingMs;
                            break;
                        case QuizPhaseKind.Judgement:
                            player.FirstGuessMs = Quiz.Room.QuizSettings.GuessMs;
                            break;
                        case QuizPhaseKind.Results:
                        case QuizPhaseKind.Looting:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (Quiz.Room.QuizSettings.TeamSize > 1)
                {
                    // also includes the player themselves
                    var teammates = Quiz.Room.Players.Where(x => x.TeamId == player.TeamId);
                    IEnumerable<string> teammateConnectionIds = ServerState.Sessions
                        .Where(x => teammates.Select(y => y.Id).Contains(x.Player.Id))
                        .Select(z => z.ConnectionId)!;

                    await HubContext.Clients.Clients(teammateConnectionIds)
                        .SendAsync("ReceivePlayerGuesses", Quiz.Room.PlayerGuesses);
                }
                else
                {
                    await HubContext.Clients.Clients(connectionId)
                        .SendAsync("ReceivePlayerGuesses", Quiz.Room.PlayerGuesses);
                }

                await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                    .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
            }
            else
            {
                // todo log invalid guess submitted
            }
        }
    }

    public async Task OnSendTogglePause()
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing &&
            !Quiz.QuizState.ExtraInfo.Contains("Waiting buffering") &&
            !Quiz.QuizState.ExtraInfo.Contains("Skipping")) // todo
        {
            if (Quiz.QuizState.IsPaused)
            {
                Quiz.QuizState.IsPaused = false;
                Quiz.Room.Log("Unpaused");
            }
            else
            {
                Quiz.QuizState.IsPaused = true;
                Quiz.Room.Log("Paused");
            }

            await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
        }
    }

    // public async Task OnSendPlayerLeaving(int playerId)
    // {
    //
    // }

    private async Task EnterLootingPhase()
    {
        var rng = Random.Shared;

        Quiz.QuizState.Phase = QuizPhaseKind.Looting;
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.LootingMs;

        TreasureRoom[][] GenerateTreasureRooms(Dictionary<string, List<Title>> validSources)
        {
            int gridSize = Quiz.Room.Players.Count switch
            {
                <= 3 => 3,
                4 or 5 => 4,
                6 or 7 => 5,
                8 or 9 => 6,
                >= 10 => 7,
            };

            TreasureRoom[][] treasureRooms =
                new TreasureRoom[gridSize].Select(_ => new TreasureRoom[gridSize]).ToArray();
            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    treasureRooms[i][j] =
                        new TreasureRoom() { Coords = new Point(i, j), Treasures = new List<Treasure>() };

                    if (j - 1 >= 0 && j - 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.North, new Point(i, j - 1));
                    }

                    if (i + 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.East, new Point(i + 1, j));
                    }

                    if (j + 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.South, new Point(i, j + 1));
                    }

                    if (i - 1 >= 0 && i - 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.West, new Point(i - 1, j));
                    }
                }
            }

            Quiz.QuizState.LootingGridSize = gridSize;

            foreach (var player in Quiz.Room.Players)
            {
                player.PlayerStatus = PlayerStatus.Looting;
                player.LootingInfo = new PlayerLootingInfo
                {
                    X = LootingConstants.TreasureRoomWidth / 2,
                    Y = LootingConstants.TreasureRoomHeight / 2,
                    Inventory = new List<Treasure>(),
                    TreasureRoomCoords =
                        new Point(rng.Next(Quiz.QuizState.LootingGridSize),
                            rng.Next(Quiz.QuizState.LootingGridSize)),
                };
            }

            const int treasureMaxX = LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize;
            const int treasureMaxY = LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize;
            foreach (var dbSong in validSources)
            {
                var treasure = new Treasure(
                    Guid.NewGuid(),
                    dbSong,
                    new Point(rng.Next(treasureMaxX), rng.Next(treasureMaxY)));

                // todo max treasures in one room?
                // todo better position randomization?
                var treasureRoomId = new Point(rng.Next(0, gridSize), rng.Next(0, gridSize));
                treasureRooms[treasureRoomId.X][treasureRoomId.Y].Treasures.Add(treasure);
            }

            // Console.WriteLine("treasureRooms: " + JsonSerializer.Serialize(treasureRooms)][ Utils.Jso);
            return treasureRooms;
        }

        Quiz.Room.TreasureRooms = GenerateTreasureRooms(Quiz.ValidSourcesForLooting);
        // await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds.Values).SendAsync("ReceivePyramidEntered");

        // todo
    }

    private async Task<bool> SetLootedSongs()
    {
        var validSources = new Dictionary<int, List<string>>();
        foreach (var player in Quiz.Room.Players)
        {
            validSources[player.Id] = new List<string>();
            foreach (var treasure in player.LootingInfo.Inventory)
            {
                validSources[player.Id].Add(treasure.ValidSource.Key);
            }

            // reduce serialized Room size & prevent Inventory leak
            player.LootingInfo = new PlayerLootingInfo();
        }

        if (!validSources.Any())
        {
            return false;
        }

        Quiz.Room.Log($"Players looted {validSources.SelectMany(x => x.Value).Distinct().Count()} distinct sources");
        var dbSongs = await DbManager.GetLootedSongs(
            Quiz.Room.QuizSettings.NumSongs,
            Quiz.Room.QuizSettings.Duplicates,
            validSources.SelectMany(x => x.Value).ToList());

        if (!dbSongs.Any())
        {
            return false;
        }

        Quiz.Room.Log($"Selected {dbSongs.Count} looted songs");

        // reduce serialized Room size
        Quiz.Room.TreasureRooms = Array.Empty<TreasureRoom[]>();

        Quiz.Songs = dbSongs;
        Quiz.QuizState.NumSongs = Quiz.Songs.Count;

        foreach (Song dbSong in dbSongs)
        {
            dbSong.Links = SongLink.FilterSongLinks(dbSong.Links);

            // todo merge this with ValidSourcesForLooting and get rid of this
            var currentSongSourceVndbUrls = dbSong.Sources
                .SelectMany(x => x.Links.Where(y => y.Type == SongSourceLinkType.VNDB))
                .Select(z => z.Url)
                .ToList();

            var lootedPlayers = validSources.Where(x => x.Value.Any(y => currentSongSourceVndbUrls.Contains(y)))
                .ToDictionary(x => x.Key, x => x.Value);

            var playerLabels = new Dictionary<int, List<Label>>();
            foreach (KeyValuePair<int, List<string>> lootedPlayer in lootedPlayers)
            {
                var newLabel = new Label
                {
                    Id = -1,
                    IsPrivate = false,
                    Name = "Looted",
                    VNs = new Dictionary<string, int> { { currentSongSourceVndbUrls.First(), -1 } },
                    Kind = LabelKind.Include
                };

                playerLabels.Add(lootedPlayer.Key, new List<Label> { newLabel });
            }

            dbSong.PlayerLabels = playerLabels;
        }

        return true;
    }

    public async Task OnSendPlayerMoved(Player player, int newX, int newY, DateTime dateTime,
        string connectionId)
    {
        // todo anti-cheat
        player.LootingInfo.X = newX;
        player.LootingInfo.Y = newY;

        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values.Where(x => x != connectionId))
            .SendAsync("ReceiveUpdatePlayerLootingInfo",
                player.Id,
                player.LootingInfo with { Inventory = new List<Treasure>() }
            );
    }

    public async Task OnSendPickupTreasure(Session session, Guid treasureGuid)
    {
        if (!Quiz.Room.TreasureRooms.Any())
        {
            return;
        }

        var player = session.Player;
        if (player.LootingInfo.TreasureRoomCoords.X < Quiz.QuizState.LootingGridSize &&
            player.LootingInfo.TreasureRoomCoords.Y < Quiz.QuizState.LootingGridSize)
        {
            var treasureRoom = Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][
                player.LootingInfo.TreasureRoomCoords.Y];
            var treasure = treasureRoom.Treasures.SingleOrDefault(x => x.Guid == treasureGuid);

            if (treasure != null)
            {
                if (treasure.Position.IsReachableFromCoords((int)player.LootingInfo.X, (int)player.LootingInfo.Y))
                {
                    if (player.LootingInfo.Inventory.Count < Quiz.Room.QuizSettings.InventorySize)
                    {
                        player.LootingInfo.Inventory.Add(treasure);
                        treasureRoom.Treasures.Remove(treasure);

                        await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                            .SendAsync("ReceiveUpdateTreasureRoom", treasureRoom);

                        await HubContext.Clients.Clients(session.ConnectionId!)
                            .SendAsync("ReceiveUpdateRemainingMs", Quiz.QuizState.RemainingMs);

                        await HubContext.Clients.Clients(session.ConnectionId!)
                            .SendAsync("ReceiveUpdatePlayerLootingInfo", player.Id, player.LootingInfo);
                    }
                }
                else
                {
                    Quiz.Room.Log(
                        $"Player is not close enough to the treasure to pickup: {player.LootingInfo.X},{player.LootingInfo.Y} -> " +
                        $"{treasure.Position.X},{treasure.Position.Y}", player.Id);
                }
            }
            else
            {
                Quiz.Room.Log("Could not find the treasure to pickup");
            }
        }
        else
        {
            Quiz.Room.Log("Invalid player treasure room coords", player.Id);
        }
    }

    public async Task OnSendDropTreasure(Session session, Guid treasureGuid)
    {
        var player = Quiz.Room.Players.Single(x => x.Id == session.Player.Id);
        var treasure = player.LootingInfo.Inventory.SingleOrDefault(x => x.Guid == treasureGuid);
        if (treasure != null)
        {
            var treasureRoom = Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][
                player.LootingInfo.TreasureRoomCoords.Y];

            int newX = (int)Math.Clamp(
                player.LootingInfo.X +
                Random.Shared.Next(-LootingConstants.PlayerAvatarSize, LootingConstants.PlayerAvatarSize),
                0, LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize);

            int newY = (int)Math.Clamp(
                player.LootingInfo.Y +
                Random.Shared.Next(-LootingConstants.PlayerAvatarSize, LootingConstants.PlayerAvatarSize),
                0, LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize);

            player.LootingInfo.Inventory.Remove(treasure);
            treasureRoom.Treasures.Add(treasure with { Position = new Point(newX, newY) });

            await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                .SendAsync("ReceiveUpdateTreasureRoom", treasureRoom);

            await HubContext.Clients.Clients(session.ConnectionId!)
                .SendAsync("ReceiveUpdateRemainingMs", Quiz.QuizState.RemainingMs);

            await HubContext.Clients.Clients(session.ConnectionId!)
                .SendAsync("ReceiveUpdatePlayerLootingInfo", player.Id, player.LootingInfo);
        }
    }

    public async Task OnSendChangeTreasureRoom(Session session, Point treasureRoomCoords, Direction direction)
    {
        var player = Quiz.Room.Players.Single(x => x.Id == session.Player.Id);

        var currentTreasureRoom =
            Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][
                player.LootingInfo.TreasureRoomCoords.Y];
        var newTreasureRoom =
            Quiz.Room.TreasureRooms[treasureRoomCoords.X][treasureRoomCoords.Y];

        if (treasureRoomCoords.X < Quiz.QuizState.LootingGridSize &&
            treasureRoomCoords.Y < Quiz.QuizState.LootingGridSize)
        {
            if (currentTreasureRoom.Exits.ContainsValue(treasureRoomCoords))
            {
                player.LootingInfo.TreasureRoomCoords = treasureRoomCoords;

                int newX = (int)player.LootingInfo.X;
                int newY = (int)player.LootingInfo.Y;
                switch (direction)
                {
                    case Direction.North:
                    case Direction.South:
                        newY = Math.Clamp((int)(LootingConstants.TreasureRoomHeight - player.LootingInfo.Y), 0,
                            LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize);
                        break;
                    case Direction.East:
                    case Direction.West:
                        newX = Math.Clamp((int)(LootingConstants.TreasureRoomWidth - player.LootingInfo.X), 0,
                            LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }

                player.LootingInfo.X = newX;
                player.LootingInfo.Y = newY;

                await HubContext.Clients.Clients(session.ConnectionId!)
                    .SendAsync("ReceiveUpdateTreasureRoom", newTreasureRoom);

                await HubContext.Clients.Clients(session.ConnectionId!)
                    .SendAsync("ReceiveUpdateRemainingMs", Quiz.QuizState.RemainingMs);

                await HubContext.Clients.Clients(session.ConnectionId!)
                    .SendAsync("ReceiveUpdatePlayerLootingInfo",
                        player.Id,
                        player.LootingInfo
                    );

                await HubContext.Clients
                    .Clients(Quiz.Room.AllConnectionIds.Values.Where(x => x != session.ConnectionId))
                    .SendAsync("ReceiveUpdatePlayerLootingInfo",
                        player.Id,
                        player.LootingInfo with { Inventory = new List<Treasure>() }
                    );
            }
            else
            {
                Quiz.Room.Log(
                    $"Failed to use non-existing exit {player.LootingInfo.TreasureRoomCoords.X},{player.LootingInfo.TreasureRoomCoords.Y} -> " +
                    $"{treasureRoomCoords.X},{treasureRoomCoords.Y}", player.Id);
                // Console.WriteLine(JsonSerializer.Serialize(Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][player.LootingInfo.TreasureRoomCoords.Y].Exits));
            }
        }
        else
        {
            Quiz.Room.Log($"Failed to move to non-existing treasure room {treasureRoomCoords}", player.Id);
        }
    }

    public async Task OnSendToggleSkip(string connectionId, int playerId)
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing &&
            Quiz.QuizState.RemainingMs > 2000 &&
            Quiz.QuizState.Phase is QuizPhaseKind.Guess or QuizPhaseKind.Results &&
            !Quiz.QuizState.IsPaused)
        {
            var player = Quiz.Room.Players.Single(x => x.Id == playerId);
            if (player.IsSkipping)
            {
                player.IsSkipping = false;
            }
            else
            {
                player.IsSkipping = true;
            }

            await HubContext.Clients.Clients(connectionId).SendAsync("ReceiveUpdateRoom", Quiz.Room, false);

            int isSkippingCount = Quiz.Room.Players.Count(x => x.IsSkipping);

            int activePlayersCount = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
                .Count(x => x.Player.HasActiveConnection);

            int skipNumber = (int)Math.Round((float)activePlayersCount * 0.8, MidpointRounding.AwayFromZero);

            Quiz.Room.Log($"isSkippingCount: {isSkippingCount}/{skipNumber}");
            if (isSkippingCount >= skipNumber)
            {
                Quiz.QuizState.RemainingMs = 500;
                Quiz.QuizState.ExtraInfo = "Skipping...";
                Quiz.Room.Log($"Skipping...");

                foreach (Player p in Quiz.Room.Players)
                {
                    p.IsSkipping = false;
                }

                await HubContext.Clients.Clients(Quiz.Room.AllConnectionIds.Values)
                    .SendAsync("ReceiveUpdateRoom", Quiz.Room, false);
            }
        }
    }

    public async Task OnConnectedAsync(int playerId, string connectionId)
    {
        if (Quiz.QuizState.QuizStatus is QuizStatus.Playing)
        {
            await OnSendPlayerIsBuffered(playerId, "OnConnectedAsync");
        }

        // todo
        // await HubContext.Clients.Clients(connectionId)
        //     .SendAsync("ReceiveRequestPlayerStatus");

        // await HubContext.Clients.Client(oldConnectionId)
        //     .SendAsync("ReceiveDisconnectSelf"); // todo should be on room page too
    }

    private Dictionary<int, List<Label>> GetPlayerLabelsForSong(Song song)
    {
        // todo handle hotjoining players
        Dictionary<int, List<Label>> playerLabels = new();

        var playerSessions = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id));
        foreach (Session session in playerSessions)
        {
            if (session.VndbInfo.Labels != null)
            {
                playerLabels[session.Player.Id] = new List<Label>();
                foreach (Label label in session.VndbInfo.Labels)
                {
                    var currentSongSourceVndbUrls = song.Sources
                        .SelectMany(x => x.Links.Where(y => y.Type == SongSourceLinkType.VNDB))
                        .Select(z => z.Url)
                        .ToList();

                    if (currentSongSourceVndbUrls.Any(x => label.VNs.ContainsKey(x)))
                    {
                        // todo? add preference for showing private labels as is
                        if (label.IsPrivate)
                        {
                            var newLabel = new Label
                            {
                                Id = -1,
                                IsPrivate = true,
                                Name = "Private Label",
                                VNs = label.VNs,
                                Kind = label.Kind
                            };
                            playerLabels[session.Player.Id].Add(newLabel);
                        }
                        else
                        {
                            var newLabel = new Label
                            {
                                Id = label.Id,
                                IsPrivate = label.IsPrivate,
                                Name = label.Name,
                                VNs = label.VNs.Where(x => currentSongSourceVndbUrls.Contains(x.Key))
                                    .ToDictionary(x => x.Key, x => x.Value),
                                Kind = label.Kind
                            };
                            playerLabels[session.Player.Id].Add(newLabel);
                        }
                    }
                }
            }
        }

        return playerLabels;
    }
}
