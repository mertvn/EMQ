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
            // Console.WriteLine("in while " + isBufferedCount);
            await Task.Delay(500);
            waitingForMs += 500;

            isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
            Quiz.QuizState.ExtraInfo = $"Waiting buffering... {isBufferedCount}/{Quiz.Room.Players.Count}";
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
            Console.WriteLine($"{Quiz.Id}-{Quiz.QuizState.sp} cA: " +
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
            Quiz.Room.Players.All(player => player.PlayerStatus == PlayerStatus.Dead))
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

            Console.WriteLine($"{Quiz.Id}-{Quiz.QuizState.sp} pG: " + player.Guess);

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
            player.IsBuffered = false;
            player.PlayerStatus = PlayerStatus.Default;

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

        var dbSongs = await DbManager.GetRandomSongs(Quiz.Room.QuizSettings.NumSongs, validSources);
        if (dbSongs.Count == 0)
        {
            return false;
        }

        Quiz.Songs = dbSongs;
        // Console.WriteLine(JsonSerializer.Serialize(Quiz.Songs));
        Quiz.QuizState.NumSongs = Quiz.Songs.Count;
        Quiz.QuizState.ExtraInfo = "Waiting buffering...";

        await EnterQuiz();
        return true;
    }

    private async Task StartQuiz()
    {
        Quiz.QuizState.QuizStatus = QuizStatus.Playing;

        // await EnterQuiz();
        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizStarted");
        await EnterGuessingPhase();
        SetTimer();
    }

    public async Task OnSendPlayerIsBuffered(int playerId)
    {
        // todo timeout
        var player = Quiz.Room.Players.Find(player => player.Id == playerId)!;
        player.IsBuffered = true;

        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        Console.WriteLine($"isBufferedCount {isBufferedCount}");
        if (Quiz.QuizState.QuizStatus == QuizStatus.Starting)
        {
            if (isBufferedCount > Quiz.Room.Players.Count / 2)
            {
                await StartQuiz();
            }
        }
        else if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            if (isBufferedCount > Quiz.Room.Players.Count / 2)
            {
                // todo
                // await StartNextSong();
            }
        }
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
}
