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
using Microsoft.AspNetCore.Components.Routing;
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
                "ReceiveCorrectAnswer", (
                    new Type[] { typeof(Song), typeof(Dictionary<int, List<Label>>), typeof(Dictionary<int, string>) },
                    async param =>
                    {
                        await OnReceiveCorrectAnswer((Song)param[0]!,
                            (Dictionary<int, List<Label>>)param[1]!,
                            (Dictionary<int, string>)param[2]!);
                    })
            },
            {
                "ReceiveUpdateRoom", (new Type[] { typeof(Room), typeof(bool) },
                    async param => { await OnReceiveUpdateRoom((Room)param[0]!, (bool)param[1]!); })
            },
            {
                "ReceivePlayerGuesses", (new Type[] { typeof(Dictionary<int, string>) },
                    async param => { await OnReceivePlayerGuesses((Dictionary<int, string>)param[0]!); })
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

        public string? Guess { get; set; }

        public float Countdown { get; set; }
        public Timer Timer { get; } = new();

        public int CurrentMasterVolume { get; set; } = -1;
    }

    public QuizPageState PageState { get; set; } = new() { };

    private Room? Room { get; set; }

    private readonly List<Song?> _clientSongs;

    private Song? _currentSong { get; set; }

    private Song? _correctAnswer { get; set; }

    private Dictionary<int, List<Label>> _correctAnswerPlayerLabels { get; set; } = new();

    private Dictionary<int, string> _playerGuesses { get; set; } = new();

    private CancellationTokenSource PreloadCancellationSource { get; set; }

    private CancellationTokenRegistration PreloadCancellationRegistration { get; set; }

    private GuessInputComponent _guessInputComponent = null!;

    private ChatComponent? _chatComponent;

    private QuizSettingsComponent? _quizSettingsComponent;

    private GenericModal? _leaveModalRef;

    private GenericModal? _returnToRoomModalRef;

    private DateTime LastSync { get; set; }

    private bool SyncInProgress { get; set; }

    private bool PhaseChangeInProgress { get; set; }

    private bool IsSpectator => Room?.Spectators.Any(x => x.Id == ClientState.Session?.Player.Id) ?? false;

    private Guid Id = Guid.NewGuid();

    private bool _isDisposed;

    private IDisposable? _locationChangingRegistration;

    protected override async Task OnInitializedAsync()
    {
        // Console.WriteLine(
        //     $"OnInitialized on Component {Id.ToString().Substring(32)} ran at {DateTime.UtcNow.ToLongTimeString()}");

        await _clientUtils.TryRestoreSession();
        var room = await _clientUtils.SyncRoom();
        if (ClientState.Session is null || room is null)
        {
            _locationChangingRegistration?.Dispose();
            _navigation.NavigateTo("/", true);
            return;
        }

        if (_isDisposed)
        {
            return;
        }

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

        bool success = false;
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
                    success = true;
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                await SyncWithServer();

                if (Room!.Quiz!.QuizState.QuizStatus is QuizStatus.Canceled or QuizStatus.Ended)
                {
                    await OnReceiveQuizCanceled();
                }
            }
        }

        // we want to send this message regardless of whether the preloading was successful or not
        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered", ClientState.Session.Player.Id,
            $"OnInitializedAsync|{success}");
        await _jsRuntime.InvokeVoidAsync("addQuizPageEventListeners");
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _locationChangingRegistration = _navigation.RegisterLocationChangingHandler(OnLocationChanging);
        }
    }

    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        Console.WriteLine(context.TargetLocation);
        if (context.TargetLocation is not "/RoomPage" or "/QuizPage")
        {
            context.PreventNavigation();
            _leaveModalRef?.Show();
        }
    }

    private async Task OnReceiveCorrectAnswer(Song correctAnswer,
        Dictionary<int, List<Label>> playerLabels,
        Dictionary<int, string> playerGuesses)
    {
        if (_isDisposed)
        {
            return;
        }

        _correctAnswer = correctAnswer;
        _correctAnswerPlayerLabels = playerLabels;
        _playerGuesses = playerGuesses;
    }

    private async Task OnReceivePlayerGuesses(Dictionary<int, string> playerGuesses)
    {
        if (_isDisposed)
        {
            return;
        }

        foreach ((int playerId, string? playerGuess) in playerGuesses)
        {
            _playerGuesses[playerId] = playerGuess;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _isDisposed = true;

            PreloadCancellationSource.Cancel();
            PreloadCancellationRegistration.Unregister();
            PreloadCancellationSource.Dispose();

            PageState.Timer.Stop();
            PageState.Timer.Elapsed -= OnTimedEvent;
            PageState.Timer.Dispose();

            _chatComponent?.Dispose();
            _locationChangingRegistration?.Dispose();
            PageState = null!;
            // Console.WriteLine("disposed quizpage");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<Song?> NextSong(int index)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/NextSong",
            new ReqNextSong(ClientState.Session!.Token, index, ClientState.Session.Player.Preferences.WantsVideo,
                ClientState.Session.Player.Preferences.LinkHost));
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
        if (_isDisposed)
        {
            return;
        }

        // Console.WriteLine(
        //     $"SyncWithServer on Component {Id.ToString().Substring(32)} ran at {DateTime.UtcNow.ToLongTimeString()}");

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
            if (_chatComponent != null)
            {
                _chatComponent.Chat = room.Chat;
                await _chatComponent.CallStateHasChanged();
            }
        }
        else if (!SyncInProgress)
        {
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
        if (_isDisposed)
        {
            return;
        }

        await SyncWithServer();
        PageState.Countdown = Room!.Quiz!.QuizState.RemainingMs;
        StateHasChanged();
        SetTimer();
    }

    private async Task OnReceiveQuizEnded()
    {
        if (_isDisposed)
        {
            return;
        }

        await SyncWithServer();
        // TODO: do endgame stuff
        await _jsRuntime.InvokeVoidAsync("removeQuizPageEventListeners");
        await Task.Delay(TimeSpan.FromSeconds(15));
        _navigation.NavigateTo("/RoomPage");
    }

    private async Task OnReceiveQuizCanceled()
    {
        if (_isDisposed)
        {
            return;
        }

        await SyncWithServer();
        await _jsRuntime.InvokeVoidAsync("removeQuizPageEventListeners");
        await Task.Delay(TimeSpan.FromSeconds(1));
        _navigation.NavigateTo("/RoomPage");
    }

    private async Task ForceReturnToRoomImmediately()
    {
        Console.WriteLine("Force returning to Room");
        // NavigationManager.NavigateTo just does nothing sometimes on Firefox
        await _jsRuntime.InvokeVoidAsync("changeLocation", $"{_navigation.BaseUri}/RoomPage");
    }

    private async Task LeaveQuiz()
    {
        // await SyncWithServer();

        await _jsRuntime.InvokeVoidAsync("removeQuizPageEventListeners");
        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerLeaving");
        // await SyncWithServer();
        _locationChangingRegistration?.Dispose();

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        _navigation.NavigateTo("/HotelPage");
    }

    public async Task OnReceivePhaseChanged(int phase)
    {
        if (_isDisposed)
        {
            return;
        }

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

                // need to clear it again here or it doesn't work(???)
                PageState.Guess = "";
                await _guessInputComponent.ClearInputField();
                _guessInputComponent.CallStateHasChanged();
                break;
            case QuizPhaseKind.Judgement:
                // send the non-Entered guess if the player hasn't sent a guess before for the current song
                if (string.IsNullOrEmpty(PageState.Guess))
                {
                    PageState.Guess = _guessInputComponent.GetSelectedText();
                }

                if (!string.IsNullOrEmpty(PageState.Guess))
                {
                    await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", PageState.Guess);
                }

                // await SyncWithServer();
                PageState.GuessesVisibility = true;
                PageState.Countdown = 0;
                StateHasChanged();

                _guessInputComponent.CallClose();
                _guessInputComponent.CallStateHasChanged();
                StateHasChanged();
                break;
            case QuizPhaseKind.Results:
                if (ClientState.Session!.Player.Preferences.RestartSongsOnResultsPhase)
                {
                    if (_currentSong != null)
                    {
                        await _jsRuntime.InvokeAsync<string>("reloadVideo", _currentSong.StartTime);
                    }
                }

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

                if (ClientState.Session!.Player.Preferences.AutoSkipResultsPhase)
                {
                    await SendToggleSkip();
                }

                if (Room!.Quiz!.QuizState.sp + Room.QuizSettings.PreloadAmount < Room.Quiz.QuizState.NumSongs)
                {
                    PreloadCancellationSource.CancelAfter(
                        TimeSpan.FromMilliseconds((float)Room.QuizSettings.TimeoutMs - 4000));
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
        if (PageState.Timer.Enabled && !_isDisposed)
        {
            PageState.ProgressValue = 100 - (100 / PageState.ProgressDivisor * PageState.Countdown);

            if (PageState.Countdown > 0)
            {
                PageState.Countdown -= Quiz.TickRate;
            }
            else if (!SyncInProgress && DateTime.UtcNow - LastSync > TimeSpan.FromSeconds(2))
            {
                await SyncWithServer();
                if (Room is null || Room.Quiz is null ||
                    Room.Quiz.QuizState.QuizStatus is QuizStatus.Canceled or QuizStatus.Ended)
                {
                    await ForceReturnToRoomImmediately();
                }
            }

            if (ClientState.Session != null &&
                PageState.CurrentMasterVolume != ClientState.Session.Player.Preferences.VolumeMaster)
            {
                PageState.CurrentMasterVolume = ClientState.Session.Player.Preferences.VolumeMaster;
                await _jsRuntime.InvokeVoidAsync("setVideoVolume", PageState.CurrentMasterVolume / 100f);
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
                Links = new List<SongLink> { new() { Url = nextSong.Links.First().Url } }
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
                    bool success = !string.IsNullOrEmpty(song.Data);
                    if (success)
                    {
                        PageState.DebugOut.Add($"preloaded: {song.Links.First().Url}");
                    }
                    else
                    {
                        PageState.DebugOut.Add($"preload cancelled: {song.Links.First().Url}");
                    }

                    // we want to send this message regardless of whether the preloading was successful or not
                    await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered",
                        ClientState.Session.Player.Id, $"Preload|{success}");
                }
                else
                {
                    _logger.LogWarning("preload failed");
                    // todo post error message to server
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

                if (Room!.Quiz!.QuizState.QuizStatus is QuizStatus.Canceled or QuizStatus.Ended)
                {
                    Console.WriteLine("Canceling preload due to quiz canceled or ended");
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
        if (_isDisposed)
        {
            return;
        }

        await SyncWithServer(room, phaseChanged);
    }

    private async Task SetGuessToTeammateGuess(string? guess)
    {
        if (Room is { Quiz: { } })
        {
            if (Room.Quiz.QuizState.QuizStatus == QuizStatus.Playing && Room.QuizSettings.TeamSize > 1)
            {
                PageState.Guess = guess;
                await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged", PageState.Guess);
                if (ClientState.Session!.Player.Preferences.AutoSkipGuessPhase)
                {
                    // todo dedup
                    await ClientState.Session!.hubConnection!.SendAsync("SendToggleSkip");
                    StateHasChanged();
                }
            }
        }
    }

    private async Task SendHotjoinQuiz()
    {
        if (Room is { Quiz: { } })
        {
            if (Room.Quiz.QuizState.QuizStatus == QuizStatus.Playing && Room.QuizSettings.IsHotjoinEnabled)
            {
                await ClientState.Session!.hubConnection!.SendAsync("SendHotjoinQuiz");
            }
        }
    }

    private async Task ReturnToRoom()
    {
        if (Room!.Owner.Id == ClientState.Session!.Player.Id)
        {
            HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/ReturnToRoom",
                new ReqReturnToRoom(ClientState.Session.Token, Room.Id));
            if (res1.IsSuccessStatusCode)
            {
                _returnToRoomModalRef?.Hide();
            }
            else
            {
                // todo display error
            }
        }
    }
}
