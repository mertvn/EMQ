using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Client.Components;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Timer = System.Timers.Timer;

namespace EMQ.Client.Pages;

public partial class QuizPage
{
    public QuizPage()
    {
        _handlers = new()
        {
            { "ReceiveQuizStarted", (Array.Empty<Type>(), async _ => { await OnReceiveQuizStarted(); }) },
            { "ReceiveQuizEnded", (Array.Empty<Type>(), async _ => { await OnReceiveQuizEnded(); }) },
            { "ReceiveQuizCanceled", (Array.Empty<Type>(), async _ => { await OnReceiveQuizCanceled(); }) },
            {
                "ReceiveCorrectAnswer", (new Type[] { typeof(Song) },
                    async param => { await OnReceiveCorrectAnswer((Song)param[0]!); })
            },
            {
                "ReceiveUpdateRoom", (new Type[] { typeof(Room), typeof(bool) },
                    async param => { await OnReceiveUpdateRoom((Room)param[0]!, (bool)param[1]!); })
            },
        };

        PageState.Timer.Stop();
        PageState.Timer.Elapsed -= OnTimedEvent;
        PageState = new QuizPageState();
        Room = null;
        _clientSongs = new List<Song?>(new Song[Room?.Quiz?.QuizState.NumSongs ?? 1000]) { };
        _currentSong = null;
        _correctAnswer = null;
        PreloadCancellationSource = new CancellationTokenSource();
        PreloadCancellationRegistration = new CancellationTokenRegistration();
    }

    private readonly Dictionary<string, (Type[] types, Func<object?[], Task> value)> _handlers;

    public class QuizPageState
    {
        public bool IsDebug { get; } = false;
        public readonly List<string> DebugOut = new() { "" };

        public float ProgressValue { get; set; } = 0;
        public float ProgressDivisor { get; set; } = 1;

        public bool VideoPlayerVisibility { get; set; }

        public bool GuessesVisibility { get; set; } = true;
        // public bool GuessInputDisabled { get; set; } = true;

        // !!!
        // IMPORTANT TODO: I BROKE GUESS RESETTING ON PHASE CHANGE SOMEHOW
        // !!!
        public string? Guess { get; set; }

        public float Countdown { get; set; }
        public Timer Timer { get; } = new();
    }

    public static QuizPageState PageState { get; set; } = new() { };

    private static Room? Room { get; set; }

    private readonly List<Song?> _clientSongs;

    private Song? _currentSong;

    private Song? _correctAnswer;

    private CancellationTokenSource PreloadCancellationSource { get; set; }

    private CancellationTokenRegistration PreloadCancellationRegistration { get; set; }

    private GuessInputComponent _guessInputComponent = null!;

    private ChatComponent? _chatComponent;

    private DateTime LastSync { get; set; }

    private bool SyncInProgress { get; set; }

    private bool PhaseChangeInProgress { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();

        // todo reset page state between quizzes
        PageState.DebugOut.Add("init QuizPage");
        if (ClientState.Session!.hubConnection is not null)
        {
            await _clientConnectionManager.SetHandlers(_handlers);
            PageState.DebugOut.Add("initialized QuizPage hubConnection handlers");
        }
        else
        {
            // todo warn error, redirect to index page
            throw new Exception();
        }

        // await Task.Delay(TimeSpan.FromSeconds(2));
        await SyncWithServer();

        if (Room!.Quiz!.QuizState.QuizStatus is QuizStatus.Canceled or QuizStatus.Ended)
        {
            await OnReceiveQuizCanceled();
        }

        await ClientState.Session.hubConnection!.SendAsync("SendPlayerJoinedQuiz");

        if (Room?.Quiz?.QuizState.QuizStatus == QuizStatus.Starting ||
            Room?.Quiz?.QuizState.QuizStatus == QuizStatus.Playing && Room?.Quiz?.QuizState.sp == -1)
        {
            Song? nextSong = await NextSong(0);
            if (nextSong is not null)
            {
                var dledSong = await DlSong(nextSong);
                if (!string.IsNullOrEmpty(dledSong.Data))
                {
                    nextSong.Data = dledSong.Data;
                    _clientSongs[0] = nextSong;
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                await SyncWithServer();

                if (Room!.Quiz!.QuizState.QuizStatus == QuizStatus.Canceled)
                {
                    await OnReceiveQuizCanceled();
                }
            }
        }

        // we want to send this message regardless of whether the preloading was successful or not
        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered", ClientState.Session.Player.Id);
    }

    private async Task OnReceiveCorrectAnswer(Song correctAnswer)
    {
        _correctAnswer = correctAnswer;
    }

    public async ValueTask DisposeAsync()
    {
        if (ClientState.Session!.hubConnection is not null)
        {
            await ClientState.Session.hubConnection.DisposeAsync();
        }
    }

    public async Task<Song?> NextSong(int index)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/NextSong",
            new ReqNextSong(ClientState.Session!.Token, index));
        if (res.IsSuccessStatusCode)
        {
            ResNextSong? nextSong = await res.Content.ReadFromJsonAsync<ResNextSong>().ConfigureAwait(false);
            if (nextSong is not null)
            {
                Song song = new Song
                {
                    StartTime = nextSong.StartTime, Links = new List<SongLink> { new() { Url = nextSong.Url } }
                };
                return song;
            }
        }
        else
        {
            // todo
        }

        return null;
    }

    private void SetTimer()
    {
        PageState.Timer.Stop();
        PageState.Timer.Elapsed -= OnTimedEvent;

        PageState.Timer.Interval = TimeSpan.FromMilliseconds(Quiz.TickRate).TotalMilliseconds;
        PageState.Timer.Elapsed += OnTimedEvent;
        PageState.Timer.AutoReset = true;
        PageState.Timer.Start();
    }

    private async Task SyncWithServer(Room? room = null, bool forcePhaseChange = false)
    {
        int? oldPhase = null;
        int? oldSp = null;
        if (Room is { Quiz: { } })
        {
            oldPhase = (int)Room.Quiz.QuizState.Phase;
            oldSp = Room.Quiz.QuizState.sp;
        }

        if (room != null)
        {
            Room = room;
            LastSync = DateTime.UtcNow;
        }
        else if (!SyncInProgress)
        {
            // Console.WriteLine("slow sync");
            // Console.WriteLine($"start slow sync {DateTime.UtcNow:O}");
            SyncInProgress = true;
            Room = await _clientUtils.SyncRoom();
            SyncInProgress = false;
            LastSync = DateTime.UtcNow;
            // Console.WriteLine($"end slow sync {DateTime.UtcNow:O}");
        }

        if (Room is { Quiz: { } })
        {
            PageState.Countdown = Room.Quiz.QuizState.RemainingMs;
            // todo ProgressDivisor

            if (!PhaseChangeInProgress || room is not null || forcePhaseChange)
            {
                // Console.WriteLine($"checking phase change");
                PhaseChangeInProgress = true;
                if (oldPhase != null)
                {
                    bool phaseChanged;
                    switch (Room.Quiz.QuizState.Phase)
                    {
                        case QuizPhaseKind.Guess:
                            phaseChanged = (int)Room.Quiz.QuizState.Phase != oldPhase && Room.Quiz.QuizState.sp > oldSp;
                            break;
                        case QuizPhaseKind.Judgement:
                            phaseChanged = (int)Room.Quiz.QuizState.Phase != oldPhase;
                            break;
                        case QuizPhaseKind.Results:
                            phaseChanged = (int)Room.Quiz.QuizState.Phase != oldPhase;
                            break;
                        case QuizPhaseKind.Looting:
                            phaseChanged = false;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (phaseChanged || forcePhaseChange)
                    {
                        Console.WriteLine(
                            $"{oldSp}{(QuizPhaseKind)oldPhase} -> " +
                            $"{Room.Quiz.QuizState.sp}{Room.Quiz.QuizState.Phase} forced: {forcePhaseChange}");
                        await OnReceivePhaseChanged((int)Room.Quiz.QuizState.Phase);
                    }
                }

                PhaseChangeInProgress = false;
            }
        }

        StateHasChanged();
    }

    private async Task OnReceiveQuizStarted()
    {
        await SyncWithServer();
        PageState.Countdown = Room!.Quiz!.QuizState.RemainingMs;
        StateHasChanged();
        SetTimer();
    }

    private async Task OnReceiveQuizEnded()
    {
        await SyncWithServer();
        // TODO: do endgame stuff
        await Task.Delay(TimeSpan.FromSeconds(15));
        _navigation.NavigateTo("/RoomPage");
    }

    private async Task OnReceiveQuizCanceled()
    {
        await SyncWithServer();
        await Task.Delay(TimeSpan.FromSeconds(1));
        _navigation.NavigateTo("/RoomPage");
    }

    private async Task Onclick_Leave()
    {
        // await SyncWithServer();

        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm",
            "Really leave? If you return your score will not be restored.");
        if (confirmed)
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendPlayerLeaving");
            // await SyncWithServer();

            // i have no idea why, but if we don't visit RoomPage first, the next room a player enters will have double timer tick etc.
            // might be because visiting RoomPage causes a new signalr connection to be established
            _navigation.NavigateTo("/RoomPage");
            _navigation.NavigateTo("/HotelPage");
        }
    }

    public async Task OnReceivePhaseChanged(int phase)
    {
        // await SyncWithServer();

        QuizPhaseKind phaseKind = (QuizPhaseKind)phase;
        switch (phaseKind)
        {
            case QuizPhaseKind.Guess:
                PreloadCancellationSource.Cancel();
                PreloadCancellationRegistration.Unregister();
                PreloadCancellationSource.Dispose();
                PreloadCancellationSource = new CancellationTokenSource();
                PreloadCancellationRegistration =
                    PreloadCancellationSource.Token.Register(() => _jsRuntime.InvokeVoidAsync("Helpers.abortFetch"));

                PageState.Guess = "";
                await _guessInputComponent.ClearInputField();

                PageState.ProgressValue = 0;
                PageState.ProgressDivisor = Room!.QuizSettings.GuessMs;
                PageState.VideoPlayerVisibility = false;

                if (!(Room.QuizSettings.TeamSize > 1))
                {
                    PageState.GuessesVisibility =
                        false; // todo: should be able to toggle players' guesses separately (for multi-team games)
                }

                _correctAnswer = null;
                PageState.Countdown = Room!.Quiz!.QuizState.RemainingMs;
                StateHasChanged();

                await SwapSongs(Room.Quiz.QuizState.sp);
                StateHasChanged();

                _guessInputComponent.CallStateHasChanged();
                break;
            case QuizPhaseKind.Judgement:
                await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", PageState.Guess);
                // await SyncWithServer();
                PageState.GuessesVisibility = true;
                PageState.Countdown = 0;
                StateHasChanged();

                _guessInputComponent.CallClose();
                _guessInputComponent.CallStateHasChanged();
                break;
            case QuizPhaseKind.Results:
                // TODO: restart song (option?)
                PageState.ProgressValue = 0;
                PageState.ProgressDivisor = Room!.QuizSettings.ResultsMs;
                PageState.VideoPlayerVisibility = true;
                StateHasChanged();

                _guessInputComponent.CallClose();
                _guessInputComponent.CallStateHasChanged();

                if (_correctAnswer == null)
                {
                    // todo request
                }

                if (Room!.Quiz!.QuizState.sp + Room.QuizSettings.PreloadAmount < Room.Quiz.QuizState.NumSongs)
                {
                    PreloadCancellationSource.CancelAfter(
                        TimeSpan.FromMilliseconds((float)Room.QuizSettings.ResultsMs * 5));
                    await Preload(Room.Quiz!.QuizState.sp, Room.QuizSettings.PreloadAmount);
                }

                break;
            case QuizPhaseKind.Looting:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (PageState.Timer.Enabled)
        {
            PageState.ProgressValue += 100 / PageState.ProgressDivisor * Quiz.TickRate;

            if (PageState.Countdown > 0)
            {
                PageState.Countdown -= Quiz.TickRate;
            }
            else if (!SyncInProgress && DateTime.UtcNow - LastSync > TimeSpan.FromSeconds(2))
            {
                await SyncWithServer();
            }

            StateHasChanged();
        }
    }

    private async Task SwapSongs(int index)
    {
        if (index < _clientSongs.Count)
        {
            PageState.DebugOut.Add("index: " + index);
            // _clientState._debug.Add("cs: " + JsonSerializer.Serialize(_clientSongs));

            if (_clientSongs.ElementAtOrDefault(index) is not null)
            {
                _currentSong = _clientSongs[index];
            }

            if (string.IsNullOrEmpty(_currentSong?.Data))
            {
                await LoadMissingSong(index);
            }

            await _jsRuntime.InvokeAsync<string>("reloadVideo", _currentSong!.StartTime);
        }
        else
        {
            _logger.LogError($"Attempted to swap to a song that does not exist -- probably desynchronized;" +
                             $" index: {index}, clientSongs.Count: {_clientSongs.Count}");
        }
    }

    private async Task LoadMissingSong(int index)
    {
        PageState.DebugOut.Add("Loading missing song");
        var nextSong = await NextSong(index);
        if (nextSong is not null)
        {
            _currentSong = new Song()
            {
                StartTime = nextSong.StartTime,
                Links = new List<SongLink>
                {
                    new()
                    {
                        Url = nextSong.Links.First().Url // todo
                    }
                }
            };
            StateHasChanged();
        }
        else
        {
            PageState.DebugOut.Add("Failed loading missing song");
        }
    }

    private async Task Preload(int index, int amount = 1)
    {
        for (int i = 1; i <= amount; i++)
        {
            if (index + i < _clientSongs.Count)
            {
                var song = await NextSong(index + i);
                if (song is not null)
                {
                    if (string.IsNullOrEmpty(song.Data))
                    {
                        song.Data = (await DlSong(song)).Data;
                    }

                    _clientSongs[index + i] = song;
                    if (string.IsNullOrEmpty(song.Data))
                    {
                        PageState.DebugOut.Add($"preload cancelled: {song.Links.First().Url}"); // todo link selection
                    }
                    else
                    {
                        PageState.DebugOut.Add($"preloaded: {song.Links.First().Url}"); // todo link selection
                    }

                    // we want to send this message regardless of whether the preloading was successful or not
                    await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered",
                        ClientState.Session.Player.Id);
                }
                else
                {
                    _logger.LogWarning("preload failed");
                }
            }
            else
            {
                _logger.LogWarning("no song to preload");
            }
        }
    }

    private async Task<Song> DlSong(Song song)
    {
        var ret = new Song { Links = song.Links, };

        try
        {
            PageState.DebugOut.Add($"downloading {song.Links.First().Url}");
            _logger.LogInformation($"Startjs {song.Links.First().Url}");

            int startSp = Room!.Quiz!.QuizState.sp;
            var task = _jsRuntime.InvokeAsync<string>("Helpers.fetchObjectUrl", PreloadCancellationSource.Token,
                song.Links.First().Url);
            while (!task.IsCompleted && !task.IsCanceled)
            {
                await Task.Delay(1000);

                await SyncWithServer();
                // Console.WriteLine($"sps {Room!.Quiz!.QuizState.sp} {startSp}");
                if (Room!.Quiz!.QuizState.sp > startSp)
                {
                    Console.WriteLine("Canceling preload due to sp change");
                    PreloadCancellationSource.Cancel();
                }

                // Console.WriteLine(Room!.Quiz!.QuizState.ExtraInfo);
            }

            _logger.LogInformation($"Endjs success: {task.IsCompletedSuccessfully}");
            PreloadCancellationRegistration.Unregister();

            if (task.IsCompletedSuccessfully)
            {
                string data = task.Result;
                ret.Data = data;
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning($"download cancelled {e}");
        }

        return ret;
    }

    private async Task SendTogglePause()
    {
        if (Room is { Quiz.QuizState.QuizStatus: QuizStatus.Playing })
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendTogglePause");
            await SyncWithServer();
        }
    }

    private async Task SendToggleSkip()
    {
        if (Room is { Quiz.QuizState.QuizStatus: QuizStatus.Playing })
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendToggleSkip");
            await SyncWithServer();
        }
    }

    private async Task OnReceiveUpdateRoom(Room room, bool phaseChanged)
    {
        await SyncWithServer(room, phaseChanged);
    }
}
