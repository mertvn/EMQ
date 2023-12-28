using System;
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
        ServerUtils.Client.BaseAddress = new Uri("https://localhost:7021/");

        for (int outerIndex = 0; outerIndex < 1000; outerIndex++)
        {
            int numPlayers = 10;
            Guid roomId = Guid.Empty;
            Session? p0Session = null;
            for (int currentPlayer = 0; currentPlayer < numPlayers; currentPlayer++)
            {
                HttpResponseMessage res = await ServerUtils.Client.PostAsJsonAsync("Auth/CreateSession",
                    new ReqCreateSession(
                        $"p{currentPlayer}",
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

                if (currentPlayer == 0)
                {
                    ReqCreateRoom req = new(session.Token, $"r0", "", new QuizSettings() { NumSongs = 40 });
                    HttpResponseMessage res1 = await ServerUtils.Client.PostAsJsonAsync("Quiz/CreateRoom", req);
                    roomId = await res1.Content.ReadFromJsonAsync<Guid>();
                    p0Session = session;
                }

                HttpResponseMessage res2 = await ServerUtils.Client.PostAsJsonAsync("Quiz/JoinRoom",
                    new ReqJoinRoom(roomId, "", session.Token));
            }

            HttpResponseMessage res3 = await ServerUtils.Client.PostAsJsonAsync("Quiz/StartQuiz",
                new ReqStartQuiz(p0Session!.Token, roomId));
        }
    }
}
