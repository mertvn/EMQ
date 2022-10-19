using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using BlazorApp1.Server.Controllers;
using BlazorApp1.Server.Hubs;
using BlazorApp1.Shared.Quiz;
using BlazorApp1.Shared.Quiz.Concrete;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp1.Server.Model;

public class Quiz
{
    private readonly IHubContext<QuizHub> _hubContext;

    private string[] ConnectionIds =>
        AuthController.Sessions.Where(x => Players.Select(y => y.Id).Contains(x.PlayerId)).Select(x => x.ConnectionId!)
            .ToArray();

    public Quiz(IHubContext<QuizHub> hubContext, QuizSettings quizSettings)
    {
        _hubContext = hubContext;
        QuizSettings = quizSettings;
        // SetupWSListeners();
    }

    // private void SetupWSListeners()
    // {
    //     _hubContext.On<bool>("ReceiveQuizStarted", async active => { await OnReceiveQuizStarted(); });
    // }

    private Timer Timer { get; set; } = new();

    private QuizSettings QuizSettings { get; set; }

    public QuizState QuizState { get; set; } = new();

    public List<Song> Songs { get; set; } = new();

    public List<Player> Players { get; set; } = new(); // todo should this be only in Room or in both classes?

    public void SetTimer()
    {
        Timer.Stop();
        Timer.Elapsed -= OnTimedEvent;

        Timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
        Timer.Elapsed += OnTimedEvent;
        Timer.AutoReset = true;
        Timer.Start();
    }

    private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (QuizState.IsActive)
        {
            if (QuizState.RemainingSeconds >= 0)
            {
                QuizState.RemainingSeconds -= 1;
            }

            if (QuizState.RemainingSeconds < 0)
            {
                Timer.Stop();

                if (QuizState.Phase is GuessPhase)
                {
                    await EnterJudgementPhase();
                }
                else if (QuizState.Phase is JudgementPhase)
                {
                    await EnterResultsPhase();
                }
                else if (QuizState.Phase is ResultsPhase)
                {
                    await EnterGuessingPhase();
                }

                Timer.Start();
            }
        }
    }

    private async Task EnterGuessingPhase()
    {
        QuizState.Phase = new GuessPhase();
        QuizState.RemainingSeconds = QuizSettings.GuessTime;
        QuizState.sp += 1;
        await _hubContext.Clients.Clients(ConnectionIds).SendAsync("ReceivePhaseChanged", QuizState.Phase.Kind);
    }

    private async Task EnterJudgementPhase()
    {
        QuizState.Phase = new JudgementPhase();
        await _hubContext.Clients.Clients(ConnectionIds).SendAsync("ReceivePhaseChanged", QuizState.Phase.Kind);
        await JudgeGuesses();
    }

    private async Task EnterResultsPhase()
    {
        QuizState.Phase = new ResultsPhase();
        QuizState.RemainingSeconds = QuizSettings.ResultsTime;
        await _hubContext.Clients.Clients(ConnectionIds).SendAsync("ReceivePhaseChanged", QuizState.Phase.Kind);

        if (QuizState.sp + 1 == Songs.Count)
        {
            await EndQuiz();
        }
    }

    private async Task JudgeGuesses()
    {
        await Task.Delay(3000);
    }

    public async Task EndQuiz()
    {
        // todo other cleanup
        QuizState.IsActive = false;
        Timer.Stop();
        Timer.Elapsed -= OnTimedEvent;

        await _hubContext.Clients.Clients(ConnectionIds).SendAsync("ReceiveQuizEnded", QuizState.IsActive);
    }

    public async Task StartQuiz()
    {
        QuizState.IsActive = true;

        await _hubContext.Clients.Clients(ConnectionIds).SendAsync("ReceiveQuizStarted", QuizState.IsActive);
        await EnterGuessingPhase();
        SetTimer();
    }

    public async Task OnSendPlayerIsReady(int playerId)
    {
        // TODO: only start quiz if all? players ready
        if (!QuizState.IsActive) // todo: && !quizEnded
        {
            await StartQuiz();
        }
    }
}
