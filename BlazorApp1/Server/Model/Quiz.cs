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
    public Quiz(IHubContext<QuizHub> hubContext, QuizSettings quizSettings, Room room)
    {
        _hubContext = hubContext;
        QuizSettings = quizSettings;
        Room = room;
    }

    private Room Room { get; }

    private readonly IHubContext<QuizHub> _hubContext;

    private Timer Timer { get; set; } = new();

    public QuizSettings QuizSettings { get; set; }

    public QuizState QuizState { get; set; } = new();

    public List<Song> Songs { get; set; } = new();

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

                switch (QuizState.Phase.Kind)
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

                Timer.Start();
            }
        }
    }

    private async Task EnterGuessingPhase()
    {
        QuizState.Phase = new GuessPhase();
        QuizState.RemainingSeconds = QuizSettings.GuessTime;
        QuizState.sp += 1;
        await _hubContext.Clients.Clients(Room.ConnectionIds).SendAsync("ReceivePhaseChanged", QuizState.Phase.Kind);
    }

    private async Task EnterJudgementPhase()
    {
        QuizState.Phase = new JudgementPhase();
        await _hubContext.Clients.Clients(Room.ConnectionIds).SendAsync("ReceivePhaseChanged", QuizState.Phase.Kind);
        await JudgeGuesses();
    }

    private async Task EnterResultsPhase()
    {
        QuizState.Phase = new ResultsPhase();
        QuizState.RemainingSeconds = QuizSettings.ResultsTime;
        await _hubContext.Clients.Clients(Room.ConnectionIds).SendAsync("ReceivePhaseChanged", QuizState.Phase.Kind);

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

        await _hubContext.Clients.Clients(Room.ConnectionIds).SendAsync("ReceiveQuizEnded", QuizState.IsActive);
    }

    public async Task StartQuiz()
    {
        QuizState.IsActive = true;

        await _hubContext.Clients.Clients(Room.ConnectionIds).SendAsync("ReceiveQuizStarted", QuizState.IsActive);
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
