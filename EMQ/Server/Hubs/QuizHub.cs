using System;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.SignalR;

namespace EMQ.Server.Hubs;

public class QuizHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var accessToken = Context.GetHttpContext()!.Request.Query["access_token"];

        var session = ServerState.Sessions.SingleOrDefault(x => accessToken == x.Token);
        if (session != null)
        {
            string? oldConnectionId = session.ConnectionId;
            string newConnectionId = Context.ConnectionId;

            if (oldConnectionId != null)
            {
                Console.WriteLine(
                    $"p{session.Player.Id} ConnectionId changed from {oldConnectionId} to {newConnectionId}");

                var room = ServerState.Rooms.SingleOrDefault(x =>
                    x.Players.Any(player => player.Id == session.Player.Id));

                if (room != null)
                {
                    // todo notify joinqueue?
                    room.Quiz?.Log($"ConnectionId changed from {oldConnectionId} to {newConnectionId}",
                        session.Player.Id);

                    room.AllPlayerConnectionIds[session.Player.Id] = newConnectionId!;
                    if (room.Quiz != null)
                    {
                        var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                        if (quizManager != null)
                        {
                            await quizManager.OnConnectedAsync(session.Player.Id, newConnectionId);
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

            session.ConnectionId = Context.ConnectionId;
        }
        else
        {
            Console.WriteLine($"Player session wasn't found for token {accessToken}");
        }
    }

    // [Authorize]
    public async Task SendPlayerJoinedQuiz()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
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
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendGuessChanged(Context.ConnectionId, session.Player.Id, guess);
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
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
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

    // todo can't unpause after reconnecting? -- works after refreshing the page though
    // [Authorize]
    public async Task SendTogglePause()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Owner.Id == session.Player.Id)
            {
                if (room.Quiz != null)
                {
                    var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                    if (quizManager != null)
                    {
                        await quizManager.OnSendTogglePause();
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

    // [Authorize]
    public void SendPlayerLeaving()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                // await quizManager.OnSendPlayerLeaving(session.Player.Id);
                Console.WriteLine($"Removing player {session.Player.Id} from room {room.Id}");
                var player = room.Players.Single(player => player.Id == session.Player.Id)!;
                room.Players.Remove(player);
                room.AllPlayerConnectionIds.Remove(player.Id);

                // todo check if there are any players left in the room
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
    public async Task SendPlayerMoved(float newX, float newY, DateTime dateTime)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendPlayerMoved(session.Player, newX, newY, dateTime, Context.ConnectionId);
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
    public async Task SendPickupTreasure(Guid treasureGuid)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendPickupTreasure(session, treasureGuid);
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
    public async Task SendDropTreasure(Guid treasureGuid)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendDropTreasure(session, treasureGuid);
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
    public async Task SendChangeTreasureRoom(Point treasureRoomId, Direction direction)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendChangeTreasureRoom(session, treasureRoomId, direction);
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
    public async Task SendToggleSkip()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                if (room.Quiz != null)
                {
                    var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                    if (quizManager != null)
                    {
                        await quizManager.OnSendToggleSkip(Context.ConnectionId, session.Player.Id);
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
