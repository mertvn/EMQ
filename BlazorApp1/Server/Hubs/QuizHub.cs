using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorApp1.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp1.Server.Hubs
{
    public class QuizHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            var session =
                ServerState.Sessions.Find(x => Context.GetHttpContext()!.Request.Query["access_token"] == x.Token);
            if (session != null)
            {
                if (session.ConnectionId != null)
                {
                    Console.WriteLine(
                        $"Player ConnectionId changed from {session.ConnectionId} to {Context.ConnectionId}");
                }

                session.ConnectionId = Context.ConnectionId;
            }
            else
            {
                throw new Exception();
            }

            return base.OnConnectedAsync();
        }

        // [Authorize]
        public async Task SendPlayerJoinedQuiz(int playerId)
        {
            var session = ServerState.Sessions.Find(x => x.ConnectionId == Context.ConnectionId);
            if (session != null)
            {
                var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
                if (room?.Quiz != null)
                {
                    var quizManager = ServerState.QuizManagers.Find(x => x.Quiz.Id == room.Quiz.Id);
                    if (quizManager != null)
                    {
                        await quizManager.OnSendPlayerJoinedQuiz(Context.ConnectionId);
                    }
                    else
                    {
                        // todo
                    }
                }
                else
                {
                    // todo
                }
            }
            else
            {
                // todo
            }
        }

        // [Authorize]
        public async Task SendGuessChanged(string guess)
        {
            var session = ServerState.Sessions.Find(x => x.ConnectionId == Context.ConnectionId);
            if (session != null)
            {
                var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
                if (room?.Quiz != null)
                {
                    var quizManager = ServerState.QuizManagers.Find(x => x.Quiz.Id == room.Quiz.Id);
                    if (quizManager != null)
                    {
                        await quizManager.OnSendGuessChanged(session.Player.Id, guess);
                    }
                    else
                    {
                        // todo
                    }
                }
                else
                {
                    // todo
                }
            }
            else
            {
                // todo
            }
        }
    }
}
