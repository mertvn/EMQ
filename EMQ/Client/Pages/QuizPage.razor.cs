using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Client.Components;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Abstract;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto;
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
                    new Type[]
                    {
                        typeof(Song), typeof(Dictionary<int, List<Label>>), typeof(Dictionary<int, PlayerGuess?>),
                        typeof(Dictionary<int, short>)
                    },
                    async param =>
                    {
                        await OnReceiveCorrectAnswer((Song)param[0]!,
                            (Dictionary<int, List<Label>>)param[1]!,
                            (Dictionary<int, PlayerGuess?>)param[2]!,
                            (Dictionary<int, short>)param[3]!);
                    })
            },
            {
                "ReceiveUpdateRoom", (new Type[] { typeof(Room), typeof(bool), typeof(DateTime) },
                    async param =>
                    {
                        await OnReceiveUpdateRoom((Room)param[0]!, (bool)param[1]!, (DateTime)param[2]!);
                    })
            },
            {
                "ReceivePlayerGuesses", (new Type[] { typeof(Dictionary<int, PlayerGuess?>) },
                    async param => { await OnReceivePlayerGuesses((Dictionary<int, PlayerGuess?>)param[0]!); })
            },
        };

        PageState.Timer.Stop();
        PageState.Timer.Elapsed -= OnTimedEvent;
        PageState = new QuizPageState();
        Room = null;
        _clientSongs = new List<Song?>(new Song[Room?.Quiz?.QuizState.NumSongs ?? 1000]) { };
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

        public PlayerGuess Guess { get; set; } = new PlayerGuess();

        public float Countdown { get; set; }

        public Timer Timer { get; } = new();

        public Dictionary<string, int> CurrentMasterVolumes { get; set; } =
            new Dictionary<string, int>() { { "video1", -1 }, { "video2", -1 } };
    }

    public QuizPageState PageState { get; set; } = new() { };

    private Room? Room { get; set; }

    private readonly List<Song?> _clientSongs;

    public Dictionary<int, ResGetPublicUserInfo> UserDetailsDict { get; set; } = new();

    private Song? _currentSong
    {
        get
        {
            if (Room != null && Room.Quiz != null)
            {
                return _clientSongs.ElementAtOrDefault(Room.Quiz.QuizState.sp);
            }

            return null;
        }
    }

    private Song? _nextSong
    {
        get
        {
            if (Room != null && Room.Quiz != null)
            {
                return _clientSongs.ElementAtOrDefault(Room.Quiz.QuizState.sp + 1);
            }

            return null;
        }
    }

    private Song? _correctAnswer => ClientSongsHistory.TryGetValue(Room?.Quiz?.QuizState.sp ?? -1, out var sh)
        ? sh.Song
        : null;

    private Dictionary<int, List<Label>> _correctAnswerPlayerLabels { get; set; } = new();

    private Dictionary<int, short> _correctAnswerPlayerVotes { get; set; } = new();

    private Dictionary<int, PlayerGuess?> _playerGuesses { get; set; } = new();

    // values are set by @ref attributes if needed
    private Dictionary<GuessKind, IAutocompleteComponent?> AutocompleteComponentDict { get; } =
        new()
        {
            { GuessKind.Mst, null },
            { GuessKind.A, null },
            { GuessKind.Mt, null },
            { GuessKind.Rigger, null },
            { GuessKind.Developer, null },
            { GuessKind.Composer, null },
            { GuessKind.Arranger, null },
            { GuessKind.Lyricist, null },
        };

    private ChatComponent? _chatComponent;

    private QuizSettingsComponent? _quizSettingsComponent;

    private GenericModal? _leaveModalRef;

    private GenericModal? _returnToRoomModalRef;

    private SongHistoryWrapperComponent? _songHistoryWrapperComponent;

    private EditBotPlayerWrapperComponent? _editBotPlayerWrapperComponent;

    private Player? _editBotPlayerModel;

    private GenericModal? _inventoryModalRef;

    private DateTime LastSync { get; set; }

    private bool SyncInProgress { get; set; }

    private bool PhaseChangeInProgress { get; set; }

    private bool IsSpectator => Room?.Spectators.Any(x => x.Id == ClientState.Session?.Player.Id) ?? false;

    private bool IsMultipleChoice => ClientState.Session != null &&
                                     Room?.Players.FirstOrDefault(x => x.Id == ClientState.Session.Player.Id)
                                         ?.AnsweringKind is AnsweringKind.MultipleChoice;

    private bool _isDisposed;

    private IDisposable? _locationChangingRegistration;

    public DateTime? QuizEndedTime { get; set; } = null;

    public DateTime LastBufferCheck { get; set; }

    public Dictionary<int, HashSet<QuizPhaseKind>> CompletedPhaseChanges { get; set; } = new();

    public string VisibleVideoElementId
    {
        get
        {
            if (Room is null || Room.Quiz is null)
            {
                return "video1";
            }

            return Room.Quiz.QuizState.sp % 2 == 0 ? "video1" : "video2";
        }
    }

    public string HiddenVideoElementId => VisibleVideoElementId == "video1" ? "video2" : "video1";

    public DateTime LastSetVideoMuted { get; set; }

    public DateTime LastSetVideoPlay { get; set; }

    private Dictionary<int, SongHistory> ClientSongsHistory { get; set; } = new();

    private int EnabledGuessKindCount { get; set; } = 1;

    protected override async Task OnInitializedAsync()
    {
        // Console.WriteLine(
        //     $"OnInitialized on Component {Id.ToString().Substring(32)} ran at {DateTime.UtcNow.ToLongTimeString()}");

        await _clientUtils.TryRestoreSession();
        await ClientUtils.SendPong(_navigation.Uri.LastSegment());

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

        if (Room?.Quiz?.QuizState.QuizStatus == QuizStatus.Starting ||
            Room?.Quiz?.QuizState.QuizStatus == QuizStatus.Playing && Room?.Quiz?.QuizState.sp == -1)
        {
            Song? nextSong = await NextSong(0);
            var startedAt = DateTime.UtcNow;

            if (nextSong is not null)
            {
                _clientSongs[0] = nextSong;
                bool success = false;
                while (!success &&
                       DateTime.UtcNow - startedAt < TimeSpan.FromMilliseconds(room.QuizSettings.TimeoutMs))
                {
                    success = await Preload2(nextSong);
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
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

        if (Room?.Quiz?.QuizState.QuizStatus == QuizStatus.Playing && Room?.Quiz?.QuizState.sp >= 0 &&
            _clientSongs.ElementAtOrDefault(Room.Quiz.QuizState.sp) is null)
        {
            Console.WriteLine($"LoadMissingSong1 @{Room.Quiz.QuizState.sp}{Room.Quiz.QuizState.Phase}");
            var song = await NextSong(Room.Quiz!.QuizState.sp);
            if (song is not null)
            {
                _clientSongs[Room.Quiz!.QuizState.sp] = song;
            }
        }

        // we want to send this message regardless of whether the preloading was successful or not
        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered", ClientState.Session.Player.Id,
            $"OnInitializedAsync");
        await _jsRuntime.InvokeVoidAsync("addQuizPageEventListeners");
    }

    protected override void OnAfterRender(bool firstRender)
    {
        // Console.WriteLine("rendered qp");
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
        Dictionary<int, PlayerGuess?> playerGuesses,
        Dictionary<int, short> playerVotes)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!ClientSongsHistory.ContainsKey(Room!.Quiz!.QuizState.sp))
        {
            ClientSongsHistory.Add(Room!.Quiz!.QuizState.sp, new SongHistory { Song = correctAnswer }); // hack
        }

        _correctAnswerPlayerLabels = playerLabels;
        _playerGuesses = playerGuesses;
        _correctAnswerPlayerVotes = playerVotes;
    }

    private async Task OnReceivePlayerGuesses(Dictionary<int, PlayerGuess?> playerGuesses)
    {
        if (_isDisposed)
        {
            return;
        }

        foreach ((int playerId, var playerGuess) in playerGuesses)
        {
            _playerGuesses[playerId] = playerGuess;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

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
            new ReqNextSong(ClientState.Session!.Token, index, ClientState.Preferences.WantsVideo,
                ClientState.Preferences.LinkHost));
        if (res.IsSuccessStatusCode)
        {
            ResNextSong? nextSong = await res.Content.ReadFromJsonAsync<ResNextSong>().ConfigureAwait(false);
            if (nextSong is not null)
            {
                Song song = new Song
                {
                    ScreenshotUrl = nextSong.ScreenshotUrl,
                    CoverUrl = nextSong.CoverUrl,
                    Sources = nextSong.Hint.Sources,
                    Artists = nextSong.Hint.Artists,
                    Titles = nextSong.Hint.Titles,
                };

                if (!Room?.QuizSettings.IsNoSoundMode ?? true)
                {
                    song.StartTime = nextSong.StartTime;
                    song.Links = new List<SongLink> { new() { Url = nextSong.Url } };
                }

                return song;
            }
        }

        return null;
    }

    private void SetTimer()
    {
        PageState.Timer.Stop();
        PageState.Timer.Elapsed -= OnTimedEvent;

        PageState.Timer.Interval = TimeSpan.FromMilliseconds(Quiz.TickRateClient).TotalMilliseconds;
        PageState.Timer.Elapsed += OnTimedEvent;
        PageState.Timer.AutoReset = true;
        PageState.Timer.Start();
    }

    private async Task SyncWithServer(Room? room = null, bool forcePhaseChange = false, DateTime? syncTime = null)
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
            LastSync = syncTime ?? DateTime.UtcNow;
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

            var res = await _clientUtils.SyncRoomWithTime();
            if (res != null && res.Time!.Value >= LastSync)
            {
                LastSync = res.Time!.Value;
                Console.WriteLine($"applied slow sync @ {LastSync:O}");
                Room = res.Room;
                if (_chatComponent != null && room != null)
                {
                    _chatComponent.Chat = room.Chat;
                    await _chatComponent.CallStateHasChanged();
                }
            }
            else
            {
                if (res?.Time != null)
                {
                    Console.WriteLine($"not applying stale slow sync; time: {res.Time.Value:O} LastSync: {LastSync:O}");
                }
            }

            SyncInProgress = false;
            // Console.WriteLine($"end slow sync {DateTime.UtcNow:O}");
        }

        if (Room is { Quiz: { } })
        {
            if (Room.Quiz.QuizState.Phase != QuizPhaseKind.Results)
            {
                PageState.VideoPlayerVisibility = false;
            }

            PageState.Countdown = Room.Quiz.QuizState.RemainingMs;
            EnabledGuessKindCount = Room.QuizSettings.EnabledGuessKinds.Count(x => x.Value);

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

                    if (!CompletedPhaseChanges.TryGetValue(Room.Quiz.QuizState.sp, out HashSet<QuizPhaseKind>? _))
                    {
                        CompletedPhaseChanges[Room.Quiz.QuizState.sp] = new HashSet<QuizPhaseKind>();
                    }

                    if (phaseChanged || forcePhaseChange)
                    {
                        if (CompletedPhaseChanges[Room.Quiz.QuizState.sp].Add(Room.Quiz.QuizState.Phase))
                        {
                            Console.WriteLine(
                                $"{DateTime.UtcNow:O} {oldSp}{(QuizPhaseKind)oldPhase} -> " +
                                $"{Room.Quiz.QuizState.sp}{Room.Quiz.QuizState.Phase} forced: {forcePhaseChange}");
                            await OnReceivePhaseChanged((int)Room.Quiz.QuizState.Phase);
                        }
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

        QuizEndedTime = DateTime.UtcNow;
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

        await _jsRuntime.InvokeVoidAsync("removeQuizPageEventListeners");
        await Task.Delay(TimeSpan.FromSeconds(1));
        await ForceReturnToRoomImmediately();
    }

    private async Task ForceReturnToRoomImmediately()
    {
        Console.WriteLine("Force returning to Room");
        // NavigationManager.NavigateTo just does nothing sometimes on Firefox
        await _jsRuntime.InvokeVoidAsync("changeLocation", $"{_navigation.BaseUri}RoomPage");
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
                foreach ((GuessKind key, string? _) in PageState.Guess.Dict)
                {
                    PageState.Guess.Dict[key] = null;
                }

                foreach ((GuessKind _, IAutocompleteComponent? value) in AutocompleteComponentDict)
                {
                    if (value != null)
                    {
                        await value.ClearInputField();
                        // value.CallStateHasChanged();
                    }
                }

                PageState.ProgressValue = 0;
                PageState.ProgressDivisor = Room!.QuizSettings.GuessMs;
                PageState.VideoPlayerVisibility = false;

                if (!(Room.QuizSettings.TeamSize > 1))
                {
                    PageState.GuessesVisibility =
                        false; // todo: should be able to toggle players' guesses separately (for multi-team games)
                }

                PageState.Countdown = Room!.Quiz!.QuizState.RemainingMs;
                StateHasChanged();

                if (_clientSongs.ElementAtOrDefault(Room.Quiz.QuizState.sp) is null)
                {
                    Console.WriteLine($"LoadMissingSong2 @{Room.Quiz.QuizState.sp}{Room.Quiz.QuizState.Phase}");
                    var song = await NextSong(Room.Quiz!.QuizState.sp);
                    if (song is not null)
                    {
                        _clientSongs[Room.Quiz!.QuizState.sp] = song;
                    }
                }

                await SwapSongs(Room.Quiz.QuizState.sp);
                StateHasChanged();

                string? focusedElementName = await _jsRuntime.InvokeAsync<string?>("getActiveElementName");
                if (focusedElementName != null &&
                    !focusedElementName.Contains("chat", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (GuessKind guessKind in Enum.GetValues<GuessKind>())
                    {
                        var comp = AutocompleteComponentDict[guessKind];
                        if (comp != null)
                        {
                            await comp.CallFocusAsync();
                            break;
                        }
                    }
                }

                break;
            case QuizPhaseKind.Judgement:
                // todo check for which answering columns to send
                // send the non-Entered guess if the player hasn't sent a guess before for the current song
                foreach ((GuessKind key, string? value) in PageState.Guess.Dict)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        string? str =
                            key is (GuessKind.A or GuessKind.Composer or GuessKind.Arranger or GuessKind.Lyricist)
                                ? Unsafe.As<AutocompleteAComponent>(
                                    AutocompleteComponentDict[key])?.MapValue(null)?.AALatinAlias
                                : AutocompleteComponentDict[key]?.GetSelectedText();
                        PageState.Guess.Dict[key] = str;
                    }

                    if (!string.IsNullOrEmpty(PageState.Guess.Dict[key]))
                    {
                        await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged",
                            PageState.Guess.Dict[key], key);
                    }
                }

                // await SyncWithServer();
                PageState.GuessesVisibility = true;
                PageState.Countdown = 0;
                StateHasChanged();

                foreach ((GuessKind _, IAutocompleteComponent? value) in AutocompleteComponentDict)
                {
                    value?.CallClose();
                    value?.CallStateHasChanged();
                }

                StateHasChanged();
                break;
            case QuizPhaseKind.Results:
                if (ClientState.Preferences.RestartSongsOnResultsPhase)
                {
                    if (_currentSong != null)
                    {
                        await _jsRuntime.InvokeAsync<string>("reloadVideo", VisibleVideoElementId,
                            _currentSong.StartTime);
                    }
                }

                if (_currentSong != null && (_currentSong.Links.FirstOrDefault()?.Url.IsVideoLink() ?? false) &&
                    _correctAnswer != null)
                {
                    if (ClientState.Preferences is { HideSpoilers: true } &&
                        _correctAnswer.Attributes.HasFlag(SongAttributes.Spoilers))
                    {
                        // todo undo this, unless player manually toggles the setting
                        ClientState.Preferences.HideVideo = true;
                    }

                    if (ClientState.Preferences is { HideFlashingLights: true } &&
                        _correctAnswer.Attributes.HasFlag(SongAttributes.FlashingLights))
                    {
                        // todo undo this, unless player manually toggles the setting
                        ClientState.Preferences.HideVideo = true;
                    }
                }

                PageState.ProgressValue = 0;
                PageState.ProgressDivisor = Room!.QuizSettings.ResultsMs;
                PageState.VideoPlayerVisibility = true;
                StateHasChanged();

                foreach ((GuessKind _, IAutocompleteComponent? value) in AutocompleteComponentDict)
                {
                    value?.CallClose();
                    value?.CallStateHasChanged();
                }

                if (_correctAnswer == null)
                {
                    // todo request
                }

                if (ClientState.Preferences.AutoSkipResultsPhase)
                {
                    await SendToggleSkip();
                }

                if (Room!.Quiz!.QuizState.sp + Room.QuizSettings.PreloadAmount < Room.Quiz.QuizState.NumSongs)
                {
                    if (Room.Quiz!.QuizState.sp + 1 < _clientSongs.Count)
                    {
                        var song = await NextSong(Room.Quiz!.QuizState.sp + 1);
                        if (song is not null)
                        {
                            _clientSongs[Room.Quiz!.QuizState.sp + 1] = song;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("no song to preload");
                    }
                }

                await OnClickButtonSongHistory(false);
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
                PageState.Countdown -= Quiz.TickRateClient;
            }
            else if (!SyncInProgress && DateTime.UtcNow - LastSync > TimeSpan.FromSeconds(2))
            {
                await SyncWithServer();
                if (Room is null || Room.Quiz is null)
                {
                    await ForceReturnToRoomImmediately();
                }
                else if (Room.Quiz.QuizState.QuizStatus is QuizStatus.Canceled or QuizStatus.Ended)
                {
                    if (QuizEndedTime is not null && DateTime.UtcNow - QuizEndedTime > TimeSpan.FromSeconds(10))
                    {
                        await ForceReturnToRoomImmediately();
                    }
                    else if (QuizEndedTime is null)
                    {
                        QuizEndedTime = DateTime.UtcNow;
                    }
                }
            }

            if (ClientState.Session != null &&
                PageState.CurrentMasterVolumes[VisibleVideoElementId] !=
                ClientState.Preferences.VolumeMaster)
            {
                PageState.CurrentMasterVolumes[VisibleVideoElementId] =
                    ClientState.Preferences.VolumeMaster;
                await _jsRuntime.InvokeVoidAsync("setVideoVolume", VisibleVideoElementId,
                    PageState.CurrentMasterVolumes[VisibleVideoElementId] / 100f);

                await _clientUtils.SavePreferencesToLocalStorage();
            }

            if (DateTime.UtcNow - LastSetVideoPlay > TimeSpan.FromMilliseconds(500))
            {
                LastSetVideoPlay = DateTime.UtcNow;
                if (Room?.Quiz != null && Room.Quiz.QuizState.sp >= 0 &&
                    Room.Quiz.QuizState.Phase == QuizPhaseKind.Guess)
                {
                    if (!await GetVideoPlaying())
                    {
                        await PlayVideo();
                    }
                }
            }

            bool _ = await Preload2(_nextSong);
            StateHasChanged();
        }
    }

    private async Task<bool> Preload2(Song? song)
    {
        if (Room?.Quiz == null || song is null)
        {
            return false;
        }

        if (song.DoneBuffering)
        {
            return true;
        }

        if (!song.Links.Any(x => x.IsFileLink)) // no sound mode
        {
            song.DoneBuffering = true;
            await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered",
                ClientState.Session.Player.Id, $"Preload2|false");
            return true;
        }

        if (DateTime.UtcNow - LastBufferCheck > TimeSpan.FromMilliseconds(500) &&
            (Room.Quiz.QuizState.Phase is QuizPhaseKind.Results ||
             Room.Quiz.QuizState.sp == -1 && Room.Quiz.QuizState.Phase is QuizPhaseKind.Guess))
        {
            LastBufferCheck = DateTime.UtcNow;

            var timeRanges = await _jsRuntime.InvokeAsync<JsTimeRange[]?>("getVideoBuffered", HiddenVideoElementId);
            // Console.WriteLine(JsonSerializer.Serialize(timeRanges, Utils.JsoIndented));

            if (timeRanges != null)
            {
                foreach (JsTimeRange timeRange in timeRanges)
                {
                    bool foundStart = false;
                    bool foundEnd = false;
                    for (int i = timeRange.start; i <= timeRange.end; i++)
                    {
                        if (i == song.StartTime)
                        {
                            foundStart = true;
                        }

                        if (i == song.StartTime + Room.QuizSettings.UI_GuessMs)
                        {
                            foundEnd = true;
                        }
                    }

                    if (foundStart && foundEnd)
                    {
                        song.DoneBuffering = true;
                        await ClientState.Session!.hubConnection!.SendAsync("SendPlayerIsBuffered",
                            ClientState.Session.Player.Id, $"Preload2|true");
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private async Task SwapSongs(int index)
    {
        if (index < _clientSongs.Count)
        {
            PageState.DebugOut.Add("index: " + index);

            if (VisibleVideoElementId == "video1")
            {
                LastSetVideoMuted = DateTime.UtcNow;
                await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video2", "muted");
                await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video1", "");
            }
            else
            {
                LastSetVideoMuted = DateTime.UtcNow;
                await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video1", "muted");
                await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video2", "");
            }

            if (_currentSong != null)
            {
                await _jsRuntime.InvokeAsync<string>("reloadVideo", VisibleVideoElementId, _currentSong.StartTime);
            }
        }
        else
        {
            _logger.LogError($"Attempted to swap to a song that does not exist -- probably desynchronized;" +
                             $" index: {index}, clientSongs.Count: {_clientSongs.Count}");
        }
    }

    private async Task SendTogglePause()
    {
        if (Room is { Quiz.QuizState.QuizStatus: QuizStatus.Playing })
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendTogglePause");
        }
    }

    private async Task SendToggleSkip()
    {
        if (Room is { Quiz.QuizState.QuizStatus: QuizStatus.Playing })
        {
            await ClientState.Session!.hubConnection!.SendAsync("SendToggleSkip");
        }
    }

    private async Task OnReceiveUpdateRoom(Room room, bool phaseChanged, DateTime time)
    {
        if (_isDisposed)
        {
            return;
        }

        if (time >= LastSync)
        {
            await SyncWithServer(room, phaseChanged, time);
        }
        else
        {
            Console.WriteLine($"rejecting stale message; time: {time:O} LastSync: {LastSync:O}");
        }
    }

    private async Task SetGuess(PlayerGuess guess)
    {
        if (Room is { Quiz: { } })
        {
            if (Room.Quiz.QuizState.QuizStatus == QuizStatus.Playing &&
                Room.Quiz.QuizState.Phase == QuizPhaseKind.Guess)
            {
                if (guess.Dict.Any(x => !string.IsNullOrWhiteSpace(x.Value)))
                {
                    PageState.Guess = guess;
                    if (Room.Quiz.MultipleChoiceOptions.Any())
                    {
                        await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged",
                            PageState.Guess.Dict[GuessKind.Mst], GuessKind.Mst);
                    }
                    else
                    {
                        foreach ((GuessKind key, IAutocompleteComponent? value) in AutocompleteComponentDict)
                        {
                            if (value != null)
                            {
                                await ClientState.Session!.hubConnection!.SendAsync("SendGuessChanged",
                                    PageState.Guess.Dict[key], key);
                            }
                        }
                    }

                    if (ClientState.Preferences.AutoSkipGuessPhase)
                    {
                        // todo dedup
                        // todo EnabledGuessKinds stuff
                        await ClientState.Session!.hubConnection!.SendAsync("SendToggleSkip");
                    }
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

    private async Task<bool> GetVideoPlaying()
    {
        bool playing = await _jsRuntime.InvokeAsync<bool>("getVideoPlaying", VisibleVideoElementId);
        return playing;
    }

    private async Task ResetVideo()
    {
        var timeRanges = await _jsRuntime.InvokeAsync<JsTimeRange[]>("getVideoBuffered", HiddenVideoElementId);
        Console.WriteLine(JsonSerializer.Serialize(timeRanges, Utils.JsoIndented));

        if (VisibleVideoElementId == "video1")
        {
            LastSetVideoMuted = DateTime.UtcNow;
            await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video2", "muted");
            await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video1", "");
        }
        else
        {
            LastSetVideoMuted = DateTime.UtcNow;
            await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video1", "muted");
            await _jsRuntime.InvokeVoidAsync("setVideoMuted", "video2", "");
        }

        if (Room is { Quiz: { } } && _currentSong != null)
        {
            await _jsRuntime.InvokeAsync<string>("resetVideo", VisibleVideoElementId,
                _currentSong.StartTime);
        }
    }

    private async Task PlayVideo()
    {
        Console.WriteLine("PlayVideo");
        await _jsRuntime.InvokeAsync<string>("playVideo", VisibleVideoElementId);
    }

    private async Task OnClickButtonSongHistory(bool showSongHistoryModal)
    {
        if (Room?.Quiz != null && (!ClientSongsHistory.TryGetValue(Room.Quiz.QuizState.sp, out var sh) ||
                                   !sh.PlayerGuessInfos.Any()))
        {
            HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/GetRoomSongHistory", Room.Id);
            if (res.IsSuccessStatusCode)
            {
                var serverSongHistory = await res.Content.ReadFromJsonAsync<Dictionary<int, SongHistory>>();
                if (serverSongHistory is not null)
                {
                    ClientSongsHistory = serverSongHistory;
                    await _songHistoryWrapperComponent!.CallStateHasChanged();
                }
            }
        }

        if (showSongHistoryModal)
        {
            await _songHistoryWrapperComponent!.Show();
        }
    }

    private async Task NGMCBurnPlayer(int playerId)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/NGMCBurnPlayer", playerId);
        if (res.IsSuccessStatusCode)
        {
            StateHasChanged();
        }
    }

    private async Task NGMCPickPlayer(int playerId)
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/NGMCPickPlayer", playerId);
        if (res.IsSuccessStatusCode)
        {
            StateHasChanged();
        }
    }

    private async Task NGMCDontBurn()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/NGMCDontBurn", "");
        if (res.IsSuccessStatusCode)
        {
            StateHasChanged();
        }
    }

    private async Task Onclick_Username(int userId)
    {
        if (UserDetailsDict.TryGetValue(userId, out _))
        {
            UserDetailsDict.Remove(userId);
        }
        else
        {
            if (userId >= Constants.PlayerIdBotMin)
            {
                _editBotPlayerModel = Room!.Players.Single(x => x.Id == userId);
                _editBotPlayerWrapperComponent!.Show();
            }
            else
            {
                HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/GetPublicUserInfo", userId);
                if (res.IsSuccessStatusCode)
                {
                    var content = (await res.Content.ReadFromJsonAsync<ResGetPublicUserInfo>())!;
                    UserDetailsDict[userId] = content;
                    StateHasChanged();
                }
            }
        }
    }

    private async Task SetAnsweringKindAsync(int value)
    {
        HttpResponseMessage res1 = await _client.PostAsJsonAsync("Quiz/SetAnsweringKind", value);
        if (res1.IsSuccessStatusCode)
        {
            await SyncWithServer();
        }
    }
}
