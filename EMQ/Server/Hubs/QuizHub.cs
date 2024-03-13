using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

        var session = ServerState.Sessions.SingleOrDefault(x => accessToken == x.Token);
        if (session != null)
        {
            session.Player.LastHeartbeatTimestamp = DateTime.UtcNow;
            string? oldConnectionId = session.ConnectionId;
            string newConnectionId = Context.ConnectionId;

            if (oldConnectionId != null)
            {
                Console.WriteLine(
                    $"p{session.Player.Id} ConnectionId changed from {oldConnectionId} to {newConnectionId}");

                var room = ServerState.Rooms.SingleOrDefault(x =>
                    x.Players.Any(player => player.Id == session.Player.Id) ||
                    x.Spectators.Any(player => player.Id == session.Player.Id));

                if (room != null)
                {
                    // todo notify joinqueue?
                    room.Log($"ConnectionId changed from {oldConnectionId} to {newConnectionId}",
                        session.Player.Id);

                    room.AllConnectionIds[session.Player.Id] = newConnectionId!;
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
            var heartbeat = Context.Features.Get<IConnectionHeartbeatFeature>();
            heartbeat!.OnHeartbeat(OnHeartbeat, session.Player);
        }
        else
        {
            Console.WriteLine($"Player session wasn't found for token {accessToken}");
        }
    }

    private static void OnHeartbeat(object obj)
    {
        ((Player)obj).LastHeartbeatTimestamp = DateTime.UtcNow;
    }

    // [Authorize]
    public async Task SendPlayerJoinedQuiz()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
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
    public async Task SendGuessChanged(string? guess)
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
    public async Task SendPlayerIsBuffered(int playerId, string source)
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
                    await quizManager.OnSendPlayerIsBuffered(session.Player.Id, source);
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
    public async Task SendPlayerLeaving()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
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
                    room.AllConnectionIds.Remove(player.Id, out _);
                    room.Log($"{player.Username} left the room.", player.Id, true);

                    if (room.Quiz != null &&
                        room.Quiz.QuizState.QuizStatus is not QuizStatus.Ended or QuizStatus.Canceled)
                    {
                        if (room.QuizSettings.GamemodeKind == GamemodeKind.NGMC)
                        {
                            var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                            if (quizManager != null)
                            {
                                room.Log("NGMC mode cannot continue if a player leaves.", -1, true);
                                await quizManager.EndQuiz();
                            }
                        }
                    }

                    if (!room.Players.Any())
                    {
                        if (room.Quiz != null)
                        {
                            var quizManager = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                            if (quizManager != null)
                            {
                                await quizManager.EndQuiz();
                            }
                        }

                        ServerState.RemoveRoom(room, "SendPlayerLeaving");
                        return;
                    }
                    else
                    {
                        if (room.Owner.Id == player.Id)
                        {
                            var newOwner = room.Players.First();
                            room.Owner = newOwner;
                            room.Log($"{newOwner.Username} is the new owner.", -1, true);
                        }
                    }
                }
                else
                {
                    var spectator = room.Spectators.Single(spectator => spectator.Id == session.Player.Id);
                    room.RemoveSpectator(spectator);
                    room.AllConnectionIds.Remove(spectator.Id, out _);
                    room.Log($"{spectator.Username} stopped spectating.", spectator.Id, true);
                }

                await Clients.Clients(Context.ConnectionId)
                    .SendAsync("ReceiveUpdateRoom", room, false, DateTime.UtcNow);
                await Clients.Clients(room.AllConnectionIds.Values)
                    .SendAsync("ReceiveUpdateRoomForRoom", room);
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
    public async Task SendPlayerMoved(int newX, int newY, DateTime dateTime)
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
            var room = ServerState.Rooms.SingleOrDefault(x =>
                x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
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

    public async Task SendHotjoinQuiz()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
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
                        await Clients.Clients(Context.ConnectionId)
                            .SendAsync("ReceiveUpdateRoom", room, false, DateTime.UtcNow);
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

    public async Task SendToggleReadiedUp()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                session.Player.IsReadiedUp = !session.Player.IsReadiedUp;
                await Clients.Clients(room.AllConnectionIds.Values)
                    .SendAsync("ReceiveUpdateRoomForRoom", room);
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

    public async Task SendConvertSpectatorToPlayerInRoom()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Spectators.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                var player = room.Spectators.SingleOrDefault(player => player.Id == session.Player.Id);
                if (player != null)
                {
                    room.AddPlayer(player);
                    room.RemoveSpectator(player);
                    room.Log($"{player.Username} converted to player.", player.Id, true);

                    await Clients.Clients(room.AllConnectionIds.Values)
                        .SendAsync("ReceiveUpdateRoomForRoom", room);
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

    public async Task SendConvertPlayerToSpectatorInRoom()
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room != null)
            {
                var player = room.Players.SingleOrDefault(player => player.Id == session.Player.Id);
                if (player != null)
                {
                    room.AddSpectator(player);
                    room.RemovePlayer(player);
                    room.Log($"{player.Username} converted to spectator.", player.Id, true);

                    await Clients.Clients(room.AllConnectionIds.Values)
                        .SendAsync("ReceiveUpdateRoomForRoom", room);
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

    public async Task SendTransferRoomOwnership(int playerId)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Owner.Id == session.Player.Id)
            {
                var targetPlayer = room.Players.SingleOrDefault(x => x.Id == playerId);
                if (targetPlayer != null)
                {
                    room.Owner = targetPlayer;
                    room.Log($"{targetPlayer.Username} is the new owner.", -1, true);
                    await Clients.Clients(room.AllConnectionIds.Values)
                        .SendAsync("ReceiveUpdateRoomForRoom", room);
                }
                else
                {
                    Console.WriteLine("Failed to find the player that is going to be the new owner.");
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

    // todo spectators?
    public async Task SendKickFromRoom(int playerId)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if (session != null)
        {
            var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            if (room?.Owner.Id == session.Player.Id)
            {
                var targetPlayer = room.Players.SingleOrDefault(x => x.Id == playerId);
                if (targetPlayer != null)
                {
                    room.RemovePlayer(targetPlayer);
                    room.AllConnectionIds.Remove(targetPlayer.Id, out string? targetPlayerConnectionId);
                    room.Log($"{targetPlayer.Username} was kicked from the room.", targetPlayer.Id, true);

                    if (!string.IsNullOrWhiteSpace(targetPlayerConnectionId))
                    {
                        await Clients.Clients(targetPlayerConnectionId)
                            .SendAsync("ReceiveKickedFromRoom");
                    }

                    await Clients.Clients(room.AllConnectionIds.Values)
                        .SendAsync("ReceiveUpdateRoomForRoom", room);
                }
                else
                {
                    Console.WriteLine("Failed to find the player that is going to be kicked.");
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
