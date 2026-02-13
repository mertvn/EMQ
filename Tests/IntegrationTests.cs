using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.SignalR.Client;
using NUnit.Framework;

// ReSharper disable UnusedVariable

namespace Tests;

public class IntegrationTests
{
    // currently broken because of auth changes
    [Test, Explicit]
    public async Task Integration_100RoomsWith1PlayerX100()
    {
        ServerUtils.Client.BaseAddress = new Uri("https://localhost:7021/");

        for (int outerIndex = 0; outerIndex < 100; outerIndex++)
        {
            HttpResponseMessage res = await ServerUtils.Client.PostAsJsonAsync("Auth/CreateSession",
                new ReqCreateSession(
                    "p0",
                    "",
                    true));

            ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
            var session = resCreateSession!.Session;
            Assert.That(!string.IsNullOrWhiteSpace(session.Token));

            var hubConnection = new HubConnectionBuilder()
                .WithUrl(new Nav(ServerUtils.Client.BaseAddress.ToString()).ToAbsoluteUri("/QuizHub"),
                    options => { options.AccessTokenProvider = () => Task.FromResult(session.Token)!; })
                .WithAutomaticReconnect()
                .Build();

            await hubConnection.StartAsync();
            Assert.That(hubConnection.State == HubConnectionState.Connected);

            for (int i = 0; i < 100; i++)
            {
                ReqCreateRoom req = new(session.Token, "Room", "", new QuizSettings() { NumSongs = 40 });
                HttpResponseMessage res1 = await ServerUtils.Client.PostAsJsonAsync("Quiz/CreateRoom", req);
                var roomId = await res1.Content.ReadFromJsonAsync<Guid>();

                HttpResponseMessage res2 = await ServerUtils.Client.PostAsJsonAsync("Quiz/JoinRoom",
                    new ReqJoinRoom(roomId, "", session.Token));

                HttpResponseMessage res3 = await ServerUtils.Client.PostAsJsonAsync("Quiz/StartQuiz",
                    new ReqStartQuiz(session.Token, roomId));
            }
        }
    }

    [Test, Explicit]
    public async Task Integration_1RoomWith10PlayersX1000()
    {
        var baseAddress = new Uri("https://localhost:7021/");

        HttpClient client = null!;
        ServerUtils.Client.BaseAddress = baseAddress;

        var dict = new Dictionary<string, HubConnection>();

        for (int outerIndex = 0; outerIndex < 50; outerIndex++)
        {
            int numPlayers = 10;
            Guid roomId = Guid.Empty;
            Session? p0Session = null;
            for (int currentPlayer = 0; currentPlayer < numPlayers; currentPlayer++)
            {
                HttpResponseMessage res = await ServerUtils.Client.PostAsJsonAsync("Auth/CreateSession",
                    new ReqCreateSession(
                        $"p{currentPlayer}",
                        "GuestGuestGuestGuest",
                        true));

                ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
                var session = resCreateSession!.Session;
                Assert.That(!string.IsNullOrWhiteSpace(session.Token));

                client = new HttpClient();
                client.BaseAddress = baseAddress;
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthStuff.AuthorizationHeaderName,
                    session.Token);
                client.Timeout = TimeSpan.FromSeconds(2);

                var hubConnection = new HubConnectionBuilder()
                    .WithUrl(new Nav(client.BaseAddress.ToString()).ToAbsoluteUri("/QuizHub"),
                        options => { options.AccessTokenProvider = () => Task.FromResult(session.Token)!; })
                    .WithAutomaticReconnect()
                    .Build();

                await hubConnection.StartAsync();
                Assert.That(hubConnection.State == HubConnectionState.Connected);
                var key = $"r{outerIndex}p{session.Player.Id}";
                dict[key] = hubConnection;

                if (currentPlayer == 0)
                {
                    ReqCreateRoom req = new(session.Token, "Room", "", new QuizSettings() { NumSongs = 40 });
                    HttpResponseMessage res1 = await client.PostAsJsonAsync("Quiz/CreateRoom", req);
                    roomId = await res1.Content.ReadFromJsonAsync<Guid>();
                    p0Session = session;

                    const int numSongs = 100;
                    var quizSettings = new QuizSettings
                    {
                        NumSongs = numSongs,
                        GuessMs = 5000,
                        UI_GuessMs = 5,
                        ResultsMs = 5000,
                        UI_ResultsMs = 5,
                        PreloadAmount = 1,
                        IsHotjoinEnabled = false,
                        TeamSize = 1,
                        Duplicates = true,
                        MaxLives = 0,
                        SongSelectionKind = SongSelectionKind.Random,
                        AnsweringKind = AnsweringKind.Typing,
                        LootingMs = 120000,
                        UI_LootingMs = 120,
                        InventorySize = 5,
                        WaitPercentage = 55,
                        TimeoutMs = 5000,
                        UI_TimeoutMs = 5,
                        Filters = new QuizFilters
                        {
                            CategoryFilters = new List<CategoryFilter>(),
                            ArtistFilters = new List<ArtistFilter>(),
                            VndbAdvsearchFilter = "",
                            VNOLangs = Enum.GetValues<Language>().ToDictionary(x => x, y => y == Language.ja),
                            SongSourceSongTypeFilters =
                                new()
                                {
                                    { SongSourceSongType.OP, new IntWrapper(0) },
                                    { SongSourceSongType.ED, new IntWrapper(0) },
                                    { SongSourceSongType.Insert, new IntWrapper(0) },
                                    { SongSourceSongType.BGM, new IntWrapper(0) },
                                    { SongSourceSongType.Random, new IntWrapper(numSongs) },
                                },
                            SongSourceSongTypeRandomEnabledSongTypes =
                                new Dictionary<SongSourceSongType, bool>
                                {
                                    { SongSourceSongType.OP, true },
                                    { SongSourceSongType.ED, true },
                                    { SongSourceSongType.Insert, true },
                                    { SongSourceSongType.BGM, true },
                                },
                            SongDifficultyLevelFilters =
                                Enum.GetValues<SongDifficultyLevel>().ToDictionary(x => x, _ => true),
                            StartDateFilter = DateTime.Parse("1988-01-01", CultureInfo.InvariantCulture),
                            EndDateFilter = DateTime.Parse("2030-01-01", CultureInfo.InvariantCulture),
                            RatingAverageStart = 100,
                            RatingAverageEnd = 1000,
                            RatingBayesianStart = 100,
                            RatingBayesianEnd = 1000,
                            VoteCountStart = 5000,
                            VoteCountEnd = 25000,
                            OnlyOwnUploads = false,
                            ScreenshotKind = ScreenshotKind.None
                        },
                        ListDistributionKind = ListDistributionKind.Random,
                        GamemodeKind = GamemodeKind.Default,
                        NGMCAllowBurning = false,
                        AllowViewingInventoryDuringQuiz = false,
                        NGMCAutoPickOnlyCorrectPlayerInTeam = false
                    };

                    HttpResponseMessage res4 = await client.PostAsJsonAsync("Quiz/ChangeRoomSettings",
                        new ReqChangeRoomSettings(
                            session.Token, roomId, quizSettings));
                }

                HttpResponseMessage res2 = await client.PostAsJsonAsync("Quiz/JoinRoom",
                    new ReqJoinRoom(roomId, "", session.Token));

                // await Task.Delay(TimeSpan.FromSeconds(17)); // 4 login attempts per minute per IP
            }

            try
            {
                client = new HttpClient();
                client.BaseAddress = baseAddress;
                client.DefaultRequestHeaders.TryAddWithoutValidation(AuthStuff.AuthorizationHeaderName,
                    p0Session!.Token);
                client.Timeout = TimeSpan.FromSeconds(2);
                HttpResponseMessage res3 = await client.PostAsJsonAsync("Quiz/StartQuiz",
                    new ReqStartQuiz(p0Session!.Token, roomId));
            }
            catch (Exception)
            {
                // ignored
            }
        }

        var token = new CancellationTokenSource();
        token.CancelAfter(TimeSpan.FromSeconds(30));
        while (!token.IsCancellationRequested)
        {
            foreach (KeyValuePair<string, HubConnection> hubConnection in dict)
            {
                await hubConnection.Value.SendAsync("SendGuessChanged", Guid.NewGuid().ToString(), GuessKind.Mst);
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        foreach (KeyValuePair<string, HubConnection> hubConnection in dict)
        {
            await hubConnection.Value.SendAsync("SendPlayerLeaving");
        }

        // ReSharper disable once FunctionNeverReturns
    }

    [Test, Explicit]
    public async Task EruModeTick()
    {
        var players = new List<Player>()
        {
            new(Random.Shared.Next(), Guid.NewGuid().ToString(), Avatar.DefaultAvatar) { TeamId = 1 },
            new(Random.Shared.Next(), Guid.NewGuid().ToString(), Avatar.DefaultAvatar) { TeamId = 1 },
            new(Random.Shared.Next(), Guid.NewGuid().ToString(), Avatar.DefaultAvatar) { TeamId = 2 },
            new(Random.Shared.Next(), Guid.NewGuid().ToString(), Avatar.DefaultAvatar) { TeamId = 2 },
            new(Random.Shared.Next(), Guid.NewGuid().ToString(), Avatar.DefaultAvatar) { TeamId = 3 },
        };

        var qs = new QuizSettings() { GamemodeKind = GamemodeKind.EruMode, MaxLives = 15, };
        var room = new Room(Guid.NewGuid(), "", players[0]) { QuizSettings = qs, };
        foreach (Player player in players)
        {
            room.Players.Enqueue(player);
        }

        var quiz = new Quiz(room, Guid.NewGuid());
        var qm = new QuizManager(quiz);
        if (await qm.PrimeQuiz())
        {
            _ = qm.StartQuiz();
            await Task.Delay(TimeSpan.FromSeconds(5));
            qm.EruModeTick();
            Console.WriteLine();
        }
    }
}
