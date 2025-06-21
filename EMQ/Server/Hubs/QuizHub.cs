using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Server.Business;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace EMQ.Server.Hubs;

public class QuizHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // In standard web APIs, bearer tokens are sent in an HTTP header.
        // However, SignalR is unable to set these headers in browsers when using some transports.
        // When using WebSockets and Server-Sent Events, the token is transmitted as a query string parameter.
        var accessToken = Context.GetHttpContext()!.Request.Query["access_token"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken = Context.GetHttpContext()!.Request.Headers[HeaderNames.Authorization].ToString()
                .Replace("Bearer ", "");
        }

        var sessions = ServerState.Sessions.Where(x => accessToken == x.Token).ToArray();
        switch (sessions.Length)
        {
            case < 1:
                {
                    Console.WriteLine($"Player session wasn't found for token {accessToken}");
                    break;
                }
            case 1:
                {
                    var session = sessions.First();
                    string newConnectionId = Context.ConnectionId;
                    Console.WriteLine($"new ConnectionId for p{session.Player.Id} = {newConnectionId}");
                    while (!session.PlayerConnectionInfos.ContainsKey(newConnectionId))
                    {
                        session.PlayerConnectionInfos.TryAdd(newConnectionId,
                            new PlayerConnectionInfo { LastHeartbeatTimestamp = DateTime.UtcNow });
                    }

                    break;
                }
            case > 1:
                {
                    // invalid state, remove all sessions
                    foreach (Session session1 in sessions)
                    {
                        await ServerState.RemoveSession(session1, "OnConnectedAsync");
                    }

                    break;
                }
        }
    }

    // [Authorize]
    public async Task SendPlayerJoinedQuiz()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x =>
                x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendPlayerJoinedQuiz(Context.ConnectionId, session.Player.Id);
                }
            }
        }
    }

    // [Authorize]
    public async Task SendGuessChanged(string? guess, GuessKind guessKind)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendGuessChanged(session.Player.Id, guess, guessKind);
                }
            }
        }
    }

    // [Authorize]
    public async Task SendPlayerIsBuffered(int playerId, string source)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendPlayerIsBuffered(session.Player.Id, source);
                }
            }
        }
    }

    // todo can't unpause after reconnecting? -- works after refreshing the page though
    // [Authorize]
    public async Task SendTogglePause()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
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
                }
            }
        }
    }

    // [Authorize]
    public async Task SendPlayerLeaving()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x =>
                x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                // await quizManager.OnSendPlayerLeaving(session.Player.Id);
                Console.WriteLine($"Removing player {session.Player.Id} from room {room.Id}");
                var player = room.Players.SingleOrDefault(player => player.Id == session.Player.Id);
                if (player != null)
                {
                    room.RemovePlayer(player);
                    await ServerState.OnPlayerLeaving(room, player);
                }
                else
                {
                    var spectator = room.Spectators.Single(spectator => spectator.Id == session.Player.Id);
                    room.RemoveSpectator(spectator);
                    room.Log($"{spectator.Username} stopped spectating.", spectator.Id, true);
                }

                TypedQuizHub.ReceiveUpdateRoom(new[] { session.Player.Id }, room, false);
                TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
            }
        }
    }

    // [Authorize]
    public async Task SendPlayerMoved(int newX, int newY, DateTime dateTime)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
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
            }
        }
    }

    // [Authorize]
    public async Task SendPickupTreasure(Guid treasureGuid)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
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
            }
        }
    }

    // [Authorize]
    public async Task SendDropTreasure(Guid treasureGuid)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
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
            }
        }
    }

    // [Authorize]
    public async Task SendChangeTreasureRoom(Point treasureRoomId, Direction direction)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x =>
                x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    await quizManager.OnSendChangeTreasureRoom(session, treasureRoomId, direction);
                }
            }
        }
    }

    // [Authorize]
    public async Task SendToggleSkip()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
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
                }
            }
        }
    }

    public async Task SendHotjoinQuiz()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Spectators.Any(y => y.Id == session.Player.Id));
            if (room?.Quiz != null)
            {
                var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                if (quizManager != null)
                {
                    if (room.QuizSettings.IsHotjoinEnabled && !room.HotjoinQueue.Contains(session.Player))
                    {
                        room.HotjoinQueue.Enqueue(session.Player);
                        TypedQuizHub.ReceiveUpdateRoom(new[] { session.Player.Id }, room, false);
                    }
                }
            }
        }
    }

    public async Task SendToggleReadiedUp()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                session.Player.IsReadiedUp = !session.Player.IsReadiedUp;
                TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
            }
        }
    }

    public async Task SendConvertSpectatorToPlayerInRoom()
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Spectators.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                var player = room.Spectators.SingleOrDefault(player => player.Id == session.Player.Id);
                if (player != null)
                {
                    room.Players.Enqueue(player);
                    room.RemoveSpectator(player);
                    room.Log($"{player.Username} converted to player.", player.Id, true);

                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                }
            }
        }
    }

    public async Task SendConvertPlayerToSpectatorInRoom(int playerId)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                var requestingPlayer = room.Players.SingleOrDefault(player => player.Id == session.Player.Id);
                var targetPlayer = room.Players.SingleOrDefault(player => player.Id == playerId);
                if (requestingPlayer != null && targetPlayer != null &&
                    (requestingPlayer.Id == room.Owner.Id || requestingPlayer.Id == targetPlayer.Id ||
                     AuthStuff.HasPermission(session, PermissionKind.Moderator)))
                {
                    room.Spectators.Enqueue(targetPlayer);
                    room.RemovePlayer(targetPlayer);
                    string byStr = requestingPlayer.Id == targetPlayer.Id ? "" : $" by {requestingPlayer.Username}";
                    room.Log($"{targetPlayer.Username} converted to spectator{byStr}.", targetPlayer.Id, true);
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id),
                        room);
                }
            }
        }
    }

    public async Task SendTransferRoomOwnership(int playerId)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null && (room.Owner.Id == session.Player.Id ||
                                 AuthStuff.HasPermission(session, PermissionKind.Moderator)))
            {
                var targetPlayer = room.Players.SingleOrDefault(x => x.Id == playerId);
                if (targetPlayer != null)
                {
                    room.Owner = targetPlayer;
                    room.Log($"{targetPlayer.Username} is the new owner (by {session.Player.Username}).", -1, true);
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                }
                else
                {
                    Console.WriteLine("Failed to find the player that is going to be the new owner.");
                }
            }
        }
    }

    // todo spectators?
    public async Task SendKickFromRoom(int playerId)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null && (room.Owner.Id == session.Player.Id ||
                                 AuthStuff.HasPermission(session, PermissionKind.Moderator)))
            {
                var targetPlayer = room.Players.SingleOrDefault(x => x.Id == playerId);
                if (targetPlayer != null)
                {
                    room.RemovePlayer(targetPlayer);
                    room.Log($"{targetPlayer.Username} was kicked from the room by {session.Player.Username}.",
                        targetPlayer.Id, true);

                    TypedQuizHub.ReceiveKickedFromRoom(new[] { targetPlayer.Id });
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                }
                else
                {
                    Console.WriteLine("Failed to find the player that is going to be kicked.");
                }
            }
        }
    }

    public async Task SendPong(Pong pong)
    {
        var session = ServerUtils.GetSessionFromConnectionId(Context.ConnectionId);
        if (session != null)
        {
            if (session.PlayerConnectionInfos.TryGetValue(Context.ConnectionId, out var pci))
            {
                pci.Page = pong.Page;
                pci.LastHeartbeatTimestamp = DateTime.UtcNow;

                session.Player.LastHeartbeatTimestamp = pci.LastHeartbeatTimestamp;
                if (Pong.QuizPages.Contains(pci.Page))
                {
                    session.Player.LastHeartbeatTimestampQuiz = pci.LastHeartbeatTimestamp;
                }
            }
        }
    }
}
