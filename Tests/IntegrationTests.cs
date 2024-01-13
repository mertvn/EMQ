using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Server;
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
                ReqCreateRoom req = new(session.Token, $"r{i}", "", new QuizSettings() { NumSongs = 40 });
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
        // baseAddress = new Uri("https://emq.crabdance.com/");

        HttpClient client = null!;
        ServerUtils.Client.BaseAddress = baseAddress;

        var dict = new Dictionary<string, HubConnection>();

        for (int outerIndex = 0; outerIndex < 10; outerIndex++)
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
                    ReqCreateRoom req = new(session.Token, $"r{outerIndex}", "", new QuizSettings() { NumSongs = 40 });
                    HttpResponseMessage res1 = await client.PostAsJsonAsync("Quiz/CreateRoom", req);
                    roomId = await res1.Content.ReadFromJsonAsync<Guid>();
                    p0Session = session;
                }

                HttpResponseMessage res2 = await client.PostAsJsonAsync("Quiz/JoinRoom",
                    new ReqJoinRoom(roomId, "", session.Token));

                await Task.Delay(TimeSpan.FromSeconds(17)); // 4 login attempts per minute per IP
            }

            HttpResponseMessage res3 = await client.PostAsJsonAsync("Quiz/StartQuiz",
                new ReqStartQuiz(p0Session!.Token, roomId));
        }

        while (true)
        {
            foreach (KeyValuePair<string, HubConnection> hubConnection in dict)
            {
                await hubConnection.Value.SendAsync("SendGuessChanged", Guid.NewGuid());
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        // ReSharper disable once FunctionNeverReturns
    }
}
