using System;
using System.Collections.Generic;
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

    private void SetTimer()
    {
        Quiz.Timer.Stop();
        Quiz.Timer.Elapsed -= OnTimedEvent;

        Quiz.Timer.Interval = TimeSpan.FromMilliseconds(Quiz.TickRate).TotalMilliseconds;
        Quiz.Timer.Elapsed += OnTimedEvent;
        Quiz.Timer.AutoReset = true;
        Quiz.Timer.Start();
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
                Quiz.Timer.Stop();

                switch (Quiz.QuizState.Phase.Kind)
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
                        await SetLootedSongs();
                        await EnterQuiz();
                        await EnterGuessingPhase();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Quiz.Timer.Start();
            }
        }
    }

    private async Task EnterGuessingPhase()
    {
        while (Quiz.QuizState.IsPaused)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        int waitingForMs = 0;
        // Console.WriteLine("ibc " + isBufferedCount);
        while (isBufferedCount < (float)Quiz.Room.Players.Count / 2 &&
               waitingForMs < Quiz.Room.QuizSettings.ResultsMs * 3)
        {
            // Console.WriteLine("in while " + isBufferedCount + "/" + (float)Quiz.Room.Players.Count / 2);
            await Task.Delay(500);
            waitingForMs += 500;

            isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
            Quiz.QuizState.ExtraInfo = $"Waiting buffering... {isBufferedCount}/{Quiz.Room.Players.Count}";
            // Console.WriteLine("ei: " + Quiz.QuizState.ExtraInfo);
        }

        Quiz.QuizState.Phase = new GuessPhase();
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.GuessMs;
        Quiz.QuizState.sp += 1;
        Quiz.QuizState.ExtraInfo = "";

        foreach (var player in Quiz.Room.Players)
        {
            player.Guess = "";
            player.PlayerStatus = PlayerStatus.Thinking;
            player.IsBuffered = false;
        }

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceivePhaseChanged", Quiz.QuizState.Phase.Kind);
    }

    private async Task EnterJudgementPhase()
    {
        Quiz.QuizState.Phase = new JudgementPhase();

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceivePhaseChanged", Quiz.QuizState.Phase.Kind);

        if (Quiz.Room.QuizSettings.TeamSize > 1)
        {
            await DetermineTeamGuesses();
        }

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

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveResyncRequired");
    }

    private bool IsGuessCorrect(string guess)
    {
        if (!CorrectAnswersDict.TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
        {
            correctAnswers = Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
                .Select(x => x.LatinTitle).ToList();
            correctAnswers.AddRange(Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
                .Select(x => x.NonLatinTitle).Where(x => x != null)!);

            CorrectAnswersDict.Add(Quiz.QuizState.sp, correctAnswers);

            // Console.WriteLine("-------");
            Console.WriteLine($"{Quiz.Id}@{Quiz.QuizState.sp} cA: " +
                              JsonSerializer.Serialize(correctAnswers, Utils.Jso));
        }

        bool correct = correctAnswers.Any(correctAnswer =>
            string.Equals(guess, correctAnswer, StringComparison.OrdinalIgnoreCase));

        return correct;
    }

    private async Task EnterResultsPhase()
    {
        Quiz.QuizState.Phase = new ResultsPhase();
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.ResultsMs;

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceiveCorrectAnswer", Quiz.Songs[Quiz.QuizState.sp]);

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceivePhaseChanged", Quiz.QuizState.Phase.Kind);

        if (Quiz.QuizState.sp + 1 == Quiz.Songs.Count ||
            (Quiz.Room.QuizSettings.MaxLives > 0 && Quiz.Room.Players.All(x => x.Lives <= 0)))
        {
            await EndQuiz();
        }

        while (Quiz.JoinQueue.Any())
        {
            var session = Quiz.JoinQueue.Dequeue();
            Quiz.Room.Players.Add(session.Player);
            Quiz.Room.AllPlayerConnectionIds.Add(session.ConnectionId!);
        }
    }

    private async Task JudgeGuesses()
    {
        await Task.Delay(TimeSpan.FromSeconds(2)); // add suspense...

        foreach (var player in Quiz.Room.Players)
        {
            if (player.PlayerStatus == PlayerStatus.Dead)
            {
                continue;
            }

            Console.WriteLine($"{Quiz.Id}@{Quiz.QuizState.sp} pG: " + player.Guess);

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
        }
    }

    public async Task EndQuiz()
    {
        // todo other cleanup
        Console.WriteLine($"Ending quiz {Quiz.Id}");
        Quiz.QuizState.QuizStatus = QuizStatus.Ended;
        Quiz.Timer.Stop();
        Quiz.Timer.Elapsed -= OnTimedEvent;
        Quiz.QuizState.ExtraInfo = "Quiz ended. Returning to room...";

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizEnded");
        // Quiz.Room.Quiz = null; // todo
    }

    private async Task EnterQuiz()
    {
        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizEntered");
        await Task.Delay(TimeSpan.FromSeconds(3));
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
            player.IsBuffered = false;
            player.PlayerStatus = PlayerStatus.Default;
            player.LootingInfo = new PlayerLootingInfo();

            if (Quiz.Room.QuizSettings.OnlyFromLists)
            {
                var session = ServerState.Sessions.Single(x => x.Player.Id == player.Id);
                if (session.VndbInfo.Labels != null)
                {
                    var include = session.VndbInfo.Labels.Where(x => x.Kind == LabelKind.Include).ToList();
                    var exclude = session.VndbInfo.Labels.Where(x => x.Kind == LabelKind.Exclude).ToList();

                    Console.WriteLine($"includeCount: {include.SelectMany(x => x.VnUrls).Count()}");
                    Console.WriteLine($"excludeCount: {exclude.SelectMany(x => x.VnUrls).Count()}");

                    if (exclude.Any())
                    {
                        foreach (Label excludedLabel in exclude)
                        {
                            var final = include.SelectMany(x => x.VnUrls.Except(excludedLabel.VnUrls));
                            validSources.AddRange(final);
                        }
                    }
                    else
                    {
                        validSources.AddRange(include.SelectMany(x => x.VnUrls));
                    }
                }
            }
        }

        validSources = validSources.Distinct().ToList();
        Console.WriteLine("validSources: " + JsonSerializer.Serialize(validSources, Utils.Jso));
        Console.WriteLine($"validSourcesCount: {validSources.Count}");

        List<Song> dbSongs;
        switch (Quiz.Room.QuizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
                dbSongs = await DbManager.GetRandomSongs(Quiz.Room.QuizSettings.NumSongs, validSources);
                if (dbSongs.Count == 0)
                {
                    return false;
                }

                Quiz.Songs = dbSongs;
                Quiz.QuizState.NumSongs = Quiz.Songs.Count;
                break;
            case SongSelectionKind.Looting:
                dbSongs = await DbManager.GetRandomSongs(
                    Quiz.Room.QuizSettings.NumSongs * ((Quiz.Room.Players.Count + 4) / 2), validSources);
                if (dbSongs.Count == 0)
                {
                    return false;
                }

                var asdf = new Dictionary<string, List<Title>>();
                foreach (Song dbSong in dbSongs)
                {
                    foreach (var dbSongSource in dbSong.Sources)
                    {
                        // todo songs with multiple vns overriding each other
                        asdf[dbSongSource.Links.First(x => x.Type == SongSourceLinkType.VNDB).Url] =
                            dbSongSource.Titles;

                        if (!dbSongSource.Titles.Any(x => x.IsMainTitle))
                        {
                            throw new Exception(JsonSerializer.Serialize(dbSongSource, Utils.Jso));
                        }
                    }
                }

                Quiz.ValidSources = asdf;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Console.WriteLine(JsonSerializer.Serialize(Quiz.Songs));
        Quiz.QuizState.ExtraInfo = "Waiting buffering...";

        return true;
    }

    public async Task StartQuiz()
    {
        Quiz.QuizState.QuizStatus = QuizStatus.Playing;

        // await EnterQuiz();
        // await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizStarted");

        switch (Quiz.Room.QuizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
                await EnterQuiz();
                await EnterGuessingPhase();
                await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizStarted");
                break;
            case SongSelectionKind.Looting:
                await EnterLootingPhase();
                await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceivePyramidEntered");
                await Task.Delay(TimeSpan.FromSeconds(3));
                await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
                    .SendAsync("ReceiveUpdateTreasureRoom", Quiz.Room.TreasureRooms[0][0]);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        SetTimer();
    }

    public async Task OnSendPlayerIsBuffered(int playerId)
    {
        Player player = Quiz.Room.Players.Single(player => player.Id == playerId);
        player.IsBuffered = true;

        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        Console.WriteLine($"isBufferedCount {isBufferedCount}");
    }

    public async Task OnSendPlayerJoinedQuiz(string connectionId, int playerId)
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            await HubContext.Clients.Clients(connectionId).SendAsync("ReceiveQuizStarted");

            var player = Quiz.Room.Players.Single(x => x.Id == playerId);
            player.Lives = Quiz.Room.QuizSettings.MaxLives;
            player.Score = 0;
            player.Guess = "";
            player.IsBuffered = false;
            player.PlayerStatus = PlayerStatus.Thinking;

            if (Quiz.Room.QuizSettings.TeamSize > 1)
            {
                var teammate = Quiz.Room.Players.First(x => x.TeamId == player.TeamId);
                player.Lives = teammate.Lives;
                player.Score = teammate.Score;
            }
        }
    }

    // todo: avoid sending other players' guesses in non-team games
    public async Task OnSendGuessChanged(string connectionId, int playerId, string guess)
    {
        if (Quiz.QuizState.Phase.Kind == QuizPhaseKind.Guess)
        {
            var player = Quiz.Room.Players.Find(x => x.Id == playerId);
            if (player != null)
            {
                player.Guess = guess;
                player.PlayerStatus = PlayerStatus.Guessed;

                if (Quiz.Room.QuizSettings.TeamSize > 1)
                {
                    await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
                        .SendAsync("ReceiveResyncRequired");
                }
                else
                {
                    await HubContext.Clients.Clients(connectionId).SendAsync("ReceiveResyncRequired");
                }
            }
            else
            {
                // todo log invalid guess submitted
            }
        }
    }

    public async Task OnSendPauseQuiz()
    {
        if (!Quiz.QuizState.ExtraInfo.Contains("Waiting buffering"))
        {
            if (Quiz.QuizState.IsPaused)
            {
                Quiz.QuizState.IsPaused = false;
                Quiz.QuizState.ExtraInfo = "";
                Console.WriteLine($"Unpaused Quiz {Quiz.Id}");
            }
            else
            {
                Quiz.QuizState.IsPaused = true;
                Quiz.QuizState.ExtraInfo = "Paused";
                Console.WriteLine($"Paused Quiz {Quiz.Id}");
            }
        }
    }

    // public async Task OnSendPlayerLeaving(int playerId)
    // {
    //
    // }

    private async Task EnterLootingPhase()
    {
        Quiz.QuizState.Phase = new LootingPhase();
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.LootingMs;

        foreach (var player in Quiz.Room.Players)
        {
            player.PlayerStatus = PlayerStatus.Looting;
            player.LootingInfo = new PlayerLootingInfo();
        }

        TreasureRoom[][] GenerateTreasureRooms(Dictionary<string, List<Title>> validSources)
        {
            var rng = Random.Shared;
            int gridSize = 3; // todo

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

            const int maxX = LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize;
            const int maxY = LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize;
            foreach (var dbSong in validSources)
            {
                var treasure = new Treasure(
                    Guid.NewGuid(),
                    dbSong,
                    new Point(rng.Next(maxX), rng.Next(maxY)));

                // todo max treasures in one room?
                // todo better position randomization?
                var treasureRoomId = new Point(rng.Next(0, gridSize), rng.Next(0, gridSize));
                treasureRooms[treasureRoomId.X][treasureRoomId.Y].Treasures.Add(treasure);
            }

            // Console.WriteLine("treasureRooms: " + JsonSerializer.Serialize(treasureRooms)][ Utils.Jso);
            return treasureRooms;
        }

        Quiz.Room.TreasureRooms = GenerateTreasureRooms(Quiz.ValidSources);
        // await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceivePyramidEntered");

        // todo
    }

    private async Task SetLootedSongs()
    {
        var validSources = new List<string>();
        foreach (var player in Quiz.Room.Players)
        {
            foreach (var treasure in player.LootingInfo.Inventory)
            {
                validSources.Add(treasure.ValidSource.Key);
            }

            // reduce serialized Room size
            // player.LootingInfo = new PlayerLootingInfo();
        }

        if (!validSources.Any())
        {
            // todo failed to prime etc.
        }

        var dbSongs = await DbManager.GetRandomSongs(Quiz.Room.QuizSettings.NumSongs, validSources);
        if (!dbSongs.Any())
        {
            // todo failed to prime etc.
        }

        // reduce serialized Room size
        // Quiz.Room.TreasureRooms = Array.Empty<TreasureRoom[]>();

        Quiz.Songs = dbSongs;
        Quiz.QuizState.NumSongs = Quiz.Songs.Count;
    }

    public async Task OnSendPlayerMoved(Player player, float newX, float newY, DateTime dateTime, string connectionId)
    {
        // todo anti-cheat
        player.LootingInfo.X = newX;
        player.LootingInfo.Y = newY;

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds.Where(x => x != connectionId))
            .SendAsync("ReceiveUpdatePlayerLootingInfo",
                player.Id,
                player.LootingInfo with { Inventory = new List<Treasure>() }
            );
    }

    public async Task OnSendPickupTreasure(Session session, Guid treasureGuid)
    {
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

                        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
                            .SendAsync("ReceiveUpdateTreasureRoom", treasureRoom);

                        await HubContext.Clients.Clients(session.ConnectionId!)
                            .SendAsync("ReceiveUpdatePlayerLootingInfo", player.Id, player.LootingInfo);
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"Player is not close enough to the treasure to pickup: {player.LootingInfo.TreasureRoomCoords.X},{player.LootingInfo.TreasureRoomCoords.Y} -> " +
                        $"{treasure.Position.X},{treasure.Position.Y}");
                }
            }
            else
            {
                Console.WriteLine("Could not find the treasure to pickup");
            }
        }
        else
        {
            Console.WriteLine("Invalid player treasure room coords");
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

            await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
                .SendAsync("ReceiveUpdateTreasureRoom", treasureRoom);

            await HubContext.Clients.Clients(session.ConnectionId!)
                .SendAsync("ReceiveUpdatePlayerLootingInfo", player.Id, player.LootingInfo);
        }
    }

    public async Task OnSendChangeTreasureRoom(Session session, Point treasureRoomCoords, Direction direction)
    {
        var player = Quiz.Room.Players.Single(x => x.Id == session.Player.Id);

        var currentTreasureRoom =
            Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][player.LootingInfo.TreasureRoomCoords.Y];
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
                    .SendAsync("ReceiveUpdatePlayerLootingInfo",
                        player.Id,
                        player.LootingInfo
                    );

                await HubContext.Clients
                    .Clients(Quiz.Room.AllPlayerConnectionIds.Where(x => x != session.ConnectionId))
                    .SendAsync("ReceiveUpdatePlayerLootingInfo",
                        player.Id,
                        player.LootingInfo with { Inventory = new List<Treasure>() }
                    );
            }
            else
            {
                Console.WriteLine(
                    $"Failed to use non-existing exit {player.LootingInfo.TreasureRoomCoords.X},{player.LootingInfo.TreasureRoomCoords.Y} -> " +
                    $"{treasureRoomCoords.X},{treasureRoomCoords.Y}");
                // Console.WriteLine(JsonSerializer.Serialize(Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][player.LootingInfo.TreasureRoomCoords.Y].Exits));
            }
        }
        else
        {
            Console.WriteLine($"Failed to move to non-existing treasure room {treasureRoomCoords}");
        }
    }
}
