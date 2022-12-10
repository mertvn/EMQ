using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Controllers;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EMQ.Server.Hubs
{
    public class QuizHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            var accessToken = Context.GetHttpContext()!.Request.Query["access_token"];

            var session = ServerState.Sessions.Find(x => accessToken == x.Token);
            if (session != null)
            {
                if (session.ConnectionId != null)
                {
                    Console.WriteLine(
                        $"Player ConnectionId changed from {session.ConnectionId} to {Context.ConnectionId}");

                    var playerRooms =
                        ServerState.Rooms.FindAll(x => x.Players.Any(player => player.Id == session.Player.Id));

                    foreach (Room playerRoom in playerRooms)
                    {
                        playerRoom.AllPlayerConnectionIds.Remove(session.ConnectionId);
                        playerRoom.AllPlayerConnectionIds.Add(Context.ConnectionId);
                        Console.WriteLine(
                            $"Notified room {playerRoom.Id} of Player ConnectionId change");
                    }
                }

                session.ConnectionId = Context.ConnectionId;
            }
            else
            {
                Console.WriteLine($"Player session wasn't found for token {accessToken}");
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
                        await quizManager.OnSendPlayerJoinedQuiz(Context.ConnectionId, session.Player.Id);
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

        // [Authorize]
        public async Task SendPlayerIsBuffered(int playerId)
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
                        await quizManager.OnSendPlayerIsBuffered(session.Player.Id);
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
        public async Task SendPauseQuiz()
        {
            var session = ServerState.Sessions.Find(x => x.ConnectionId == Context.ConnectionId);
            if (session != null)
            {
                var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
                if (room.Owner.Id == session.Player.Id)
                {
                    if (room?.Quiz != null)
                    {
                        var quizManager = ServerState.QuizManagers.Find(x => x.Quiz.Id == room.Quiz.Id);
                        if (quizManager != null)
                        {
                            await quizManager.OnSendPauseQuiz();
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
            else
            {
                // todo
            }
        }
    }
}
