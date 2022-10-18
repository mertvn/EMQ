using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using BlazorApp1.Server.Hubs;
using BlazorApp1.Shared.Quiz;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp1.Server.Model;

public class Quiz
{
    private readonly IHubContext<QuizHub> _hubContext;

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
        if (QuizState.Active)
        {
            if (QuizState.RemainingSeconds >= 0)
            {
                QuizState.RemainingSeconds -= 1;
            }

            if (QuizState.RemainingSeconds < 0)
            {
                Timer.Stop();

                if (QuizState.Phase == 0)
                {
                    await EnterJudgementPhase();
                }
                else if (QuizState.Phase == 1)
                {
                    await EnterResultsPhase();
                }
                else if (QuizState.Phase == 2)
                {
                    await EnterGuessingPhase();
                }
                else if (QuizState.Phase == 3)
                {
                    // should never get here?
                }

                Timer.Start();
            }
        }
    }

    private async Task EnterGuessingPhase()
    {
        QuizState.Phase = 0;
        QuizState.RemainingSeconds = QuizSettings.GuessTime;
        QuizState.sp += 1;
        await _hubContext.Clients.All.SendAsync("ReceivePhaseChanged", 0);
    }

    private async Task EnterJudgementPhase()
    {
        QuizState.Phase = 1;
        await _hubContext.Clients.All.SendAsync("ReceivePhaseChanged", 1);
        await JudgeGuesses();
    }

    private async Task EnterResultsPhase()
    {
        QuizState.Phase = 2;
        QuizState.RemainingSeconds = QuizSettings.ResultsTime;
        await _hubContext.Clients.All.SendAsync("ReceivePhaseChanged", 2);

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
        QuizState.Active = false;
        Timer.Stop();
        Timer.Elapsed -= OnTimedEvent;


        // todo not sure if we should treat game ending as a phase or not
        await _hubContext.Clients.All.SendAsync("ReceivePhaseChanged", 3);
        //await _hubContext.Clients.All.SendAsync("ReceiveQuizEnded", QuizState.Active);
    }

    public async Task StartQuiz()
    {
        QuizState.Active = true;
        await _hubContext.Clients.All.SendAsync("ReceiveQuizStarted", QuizState.Active);
        await EnterGuessingPhase();
        SetTimer();
    }

    public async Task OnSendPlayerIsReady()
    {
        // TODO: only start quiz if all? players ready
        if (!QuizState.Active)
        {
            await StartQuiz();
        }
    }
}
