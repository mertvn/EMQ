using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
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
        }

        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceivePhaseChanged", Quiz.QuizState.Phase.Kind);
    }

    private async Task EnterJudgementPhase()
    {
        Quiz.QuizState.Phase = new JudgementPhase();
        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds)
            .SendAsync("ReceivePhaseChanged", Quiz.QuizState.Phase.Kind);
        await JudgeGuesses();
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
        await Task.Delay(TimeSpan.FromSeconds(2)); // wait for late guesses & add suspense...

        var correctAnswers = Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
            .Select(x => x.LatinTitle).ToList();
        correctAnswers.AddRange(Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
            .Select(x => x.NonLatinTitle).Where(x => x != null)!);

        Console.WriteLine("-------");
        Console.WriteLine("cA: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));

        // todo make sure players leaving in the middle of judgement does not cause issues
        foreach (var player in Quiz.Room.Players)
        {
            Console.WriteLine("pG: " + player.Guess);

            bool correct = correctAnswers.Any(correctAnswer =>
                player.Guess?.ToLowerInvariant() == correctAnswer.ToLowerInvariant());

            if (correct)
            {
                player.IsCorrect = true;
                player.Score += 1;
                player.PlayerState = PlayerState.Correct;
            }
            else
            {
                player.IsCorrect = false;
                // player.Lives -= 1; // todo
                player.PlayerState = PlayerState.Wrong;
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

    public async Task StartQuiz()
    {
        // todo check if songs.count == 0 somewhere and if it is return to room
        Quiz.QuizState.QuizStatus = QuizStatus.Playing;

        await EnterQuiz();
        await HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds).SendAsync("ReceiveQuizStarted");
        await EnterGuessingPhase();
        SetTimer();
    }

    public async Task OnSendPlayerJoinedQuiz(string connectionId)
    {
        // TODO: only start quiz if all? players ready
        if (Quiz.QuizState.QuizStatus == QuizStatus.Starting)
        {
            await StartQuiz();
        }
        else if (Quiz.QuizState.QuizStatus != QuizStatus.Ended)
        {
            await HubContext.Clients.Clients(connectionId).SendAsync("ReceiveQuizStarted");
        }
        else
        {
            // todo warn quiz is already over
        }
    }

    public async Task OnSendGuessChanged(int playerId, string guess)
    {
        if (Quiz.QuizState.Phase.Kind != QuizPhaseKind.Results)
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
        else
        {
            // todo log invalid guess submitted
        }
    }
}
