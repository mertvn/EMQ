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

    // private string[] AllPlayerConnectionIds =>
    //     ServerState.Sessions.Where(x => Quiz.Room.Players.Select(y => y.Id).Contains(x.Player.Id))
    //         .Select(x => x.ConnectionId!).ToArray();

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
        Quiz.QuizState.Phase = new GuessPhase();
        Quiz.QuizState.RemainingMs = Quiz.QuizSettings.GuessMs;
        Quiz.QuizState.sp += 1;
        foreach (var player in Quiz.Room.Players)
        {
            player.IsCorrect = null;
            player.PlayerState = PlayerState.Thinking;
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

        if (Quiz.QuizSettings.TeamSize > 1)
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
        // todo cache correctAnswers per song
        var correctAnswers = Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
            .Select(x => x.LatinTitle).ToList();
        correctAnswers.AddRange(Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
            .Select(x => x.NonLatinTitle).Where(x => x != null)!);

        Console.WriteLine("-------");
        Console.WriteLine("cA: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));

        bool correct = correctAnswers.Any(correctAnswer =>
            string.Equals(guess, correctAnswer, StringComparison.InvariantCultureIgnoreCase));

        return correct;
    }

    private async Task EnterResultsPhase()
    {
        Quiz.QuizState.Phase = new ResultsPhase();
        Quiz.QuizState.RemainingMs = Quiz.QuizSettings.ResultsMs;
        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceiveCorrectAnswer", Quiz.Songs[Quiz.QuizState.sp]);

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceivePhaseChanged", Quiz.QuizState.Phase.Kind);

        if (Quiz.QuizState.sp + 1 == Quiz.Songs.Count)
        {
            await EndQuiz();
        }
    }

    private async Task JudgeGuesses()
    {
        await Task.Delay(TimeSpan.FromSeconds(2)); // add suspense...

        // todo make sure players leaving in the middle of judgement does not cause issues
        foreach (var player in Quiz.Room.Players)
        {
            Console.WriteLine("pG: " + player.Guess);

            bool correct = IsGuessCorrect(player.Guess);
            if (correct)
            {
                player.IsCorrect = true;
                player.Score += 1;
                player.PlayerState = PlayerState.Correct;
            }
            else
            {
                player.IsCorrect = false;
                player.PlayerState = PlayerState.Wrong;

                if (player.Lives > 0)
                {
                    player.Lives -= 1;
                    if (player.Lives == 0)
                    {
                        // todo gameover for player
                    }
                }
            }
        }
    }

    public async Task EndQuiz()
    {
        // todo other cleanup
        Quiz.QuizState.QuizStatus = QuizStatus.Ended;
        Quiz.Timer.Stop();
        Quiz.Timer.Elapsed -= OnTimedEvent;

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizEnded");
    }

    private async Task EnterQuiz()
    {
        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizEntered");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async Task<bool> PrimeQuiz()
    {
        var dbSongs = await DbManager.GetRandomSongs(Quiz.QuizSettings.NumSongs);
        Quiz.Songs = dbSongs;
        // Console.WriteLine(JsonSerializer.Serialize(Quiz.Songs));
        Quiz.QuizState.NumSongs = Quiz.Songs.Count;

        if (Quiz.QuizState.NumSongs == 0)
        {
            return false;
        }

        foreach (Player player in Quiz.Room.Players)
        {
            player.Lives = Quiz.QuizSettings.MaxLives;
        }

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

    public async Task OnSendPlayerJoinedQuiz(string connectionId)
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            await HubContext.Clients.Clients(connectionId).SendAsync("ReceiveQuizStarted");
        }
    }

    public async Task OnSendGuessChanged(int playerId, string guess)
    {
        if (Quiz.QuizState.Phase.Kind == QuizPhaseKind.Guess)
        {
            var player = Quiz.Room.Players.Find(x => x.Id == playerId);
            if (player != null)
            {
                player.Guess = guess;
                player.PlayerState = PlayerState.Guessed;
                await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveResyncRequired");
            }
            else
            {
                // todo log invalid guess submitted
            }
        }
    }
}
