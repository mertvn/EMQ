using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using EMQ.Server.Business;
using EMQ.Server.Hubs;
using EMQ.Shared.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class QuizController : ControllerBase
{
    private readonly ILogger<QuizController> _logger;
    private readonly IHubContext<QuizHub> _hubContext;

    public QuizController(ILogger<QuizController> logger, IHubContext<QuizHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    [HttpGet]
    [Route("SyncRoom")]
    public Room? SyncRoom([FromQuery] string token)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == token);
        if (session is null)
        {
            // _logger.LogError("Session not found for playerToken: " + token);
            return null;
        }

        var room = ServerState.Rooms.SingleOrDefault(x =>
            x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
        if (room is null)
        {
            // _logger.LogError("Room not found with playerToken: " + token);
            return null;
        }

        // _logger.LogError(JsonSerializer.Serialize(room));
        return room;
    }

    [HttpGet]
    [Route("SyncChat")]
    public ConcurrentQueue<ChatMessage>? SyncChat([FromQuery] string token)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == token);
        if (session is null)
        {
            // _logger.LogError("Session not found for playerToken: " + token);
            return null;
        }

        var room = ServerState.Rooms.SingleOrDefault(x =>
            x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
        if (room is null)
        {
            // _logger.LogError("Room not found with playerToken: " + token);
            return null;
        }

        // _logger.LogError(JsonSerializer.Serialize(room));
        return room.Chat;
    }

    [HttpPost]
    [Route("NextSong")]
    public ActionResult<ResNextSong> NextSong([FromBody] ReqNextSong req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var room = ServerState.Rooms.SingleOrDefault(x =>
            x.Players.Any(y => y.Id == session.Player.Id) || x.Spectators.Any(y => y.Id == session.Player.Id));
        if (room is not null)
        {
            if (room.Quiz != null)
            {
                if (req.SongIndex <= room.Quiz.QuizState.sp + room.Quiz.Room.QuizSettings.PreloadAmount)
                {
                    if (req.SongIndex < room.Quiz.Songs.Count)
                    {
                        var song = room.Quiz.Songs[req.SongIndex];

                        string? url;
                        if (req.WantsVideo)
                        {
                            url = song.Links.FirstOrDefault(x => x.Type == req.Host && x.IsVideo)?.Url;
                        }
                        else
                        {
                            url = song.Links.FirstOrDefault(x => x.Type == req.Host && !x.IsVideo)?.Url;
                        }

                        // todo priority setting for host or video
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            url = song.Links.FirstOrDefault(x => x.Type == req.Host)?.Url;
                        }

                        if (string.IsNullOrWhiteSpace(url))
                        {
                            url = song.Links.First().Url;
                        }

                        return new ResNextSong(req.SongIndex, url, song.StartTime);
                    }
                    else
                    {
                        _logger.LogError("Requested song index is invalid: " + req.SongIndex);
                        return BadRequest("Requested song index is invalid: " + req.SongIndex);
                    }
                }
                else
                {
                    _logger.LogError("Requested song index is too far in the future: " + req.SongIndex);
                    return BadRequest("Requested song index is too far in the future: " + req.SongIndex);
                }
            }
            else
            {
                _logger.LogError("Room does not have a quiz initialized: " + room.Id);
                return BadRequest("Room does not have a quiz initialized: " + room.Id);
            }
        }
        else
        {
            _logger.LogError("Room not found with playerToken: " + req.PlayerToken);
            return BadRequest("Room not found with playerToken: " + req.PlayerToken);
        }
    }

    [HttpGet]
    [Route("GetRooms")]
    public IEnumerable<Room> GetRooms()
    {
        return ServerState.Rooms;
    }

    [HttpPost]
    [Route("CreateRoom")]
    public ActionResult<int> CreateRoom([FromBody] ReqCreateRoom req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var owner = session.Player;
        var room = new Room(Random.Shared.Next(), req.Name, owner)
        {
            Password = req.Password, QuizSettings = req.QuizSettings
        };
        ServerState.AddRoom(room);
        _logger.LogInformation("Created room {room.Id} {room.Name}", room.Id, room.Name);

        return room.Id;
    }

    [HttpPost]
    [Route("JoinRoom")]
    public async Task<ActionResult<ResJoinRoom>> JoinRoom([FromBody] ReqJoinRoom req)
    {
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == req.RoomId);
        var session = ServerState.Sessions.SingleOrDefault(x => x.Player.Id == req.PlayerId);

        if (room is null || session is null)
        {
            // todo warn error
            throw new Exception();
        }

        var player = session.Player;
        if (string.IsNullOrWhiteSpace(room.Password) || room.Password == req.Password)
        {
            if (room.Players.Any(x => x.Id == req.PlayerId) || room.Spectators.Any(x => x.Id == req.PlayerId))
            {
                // TODO we really shouldn't allow this (we should handle players manually changing pages better)
                return new ResJoinRoom(room.Quiz?.QuizState.QuizStatus ?? QuizStatus.Starting);
            }

            var oldRoom = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == req.PlayerId));
            if (oldRoom is not null)
            {
                _logger.LogInformation($"Removed player {req.PlayerId} from room " + oldRoom.Id);
                oldRoom.RemovePlayer(player);
                oldRoom.AllConnectionIds.Remove(player.Id, out _);
                room.Log($"{player.Username} left the room.", -1, true);
            }

            if (room.CanJoinDirectly)
            {
                _logger.LogInformation("Added p{req.PlayerId} to r{room.Id}", req.PlayerId, room.Id);
                room.Players.Enqueue(player);
                room.AllConnectionIds[player.Id] = session.ConnectionId!;

                // we don't want to show this message right after room creation
                if (room.Players.Count > 1)
                {
                    room.Log($"{player.Username} joined the room.", -1, true);
                }
            }
            else
            {
                _logger.LogInformation("Added p{req.PlayerId} to r{room.Id} as a spectator", req.PlayerId, room.Id);
                room.Spectators.Enqueue(player);
                room.AllConnectionIds[player.Id] = session.ConnectionId!;
                room.Log($"{player.Username} started spectating.", -1, true);
            }

            // let every other player in the room know that a new player joined,
            // we can't send this message to the joining player because their room page hasn't initialized yet
            await _hubContext.Clients.Clients(room.AllConnectionIds
                    .Where(x => x.Value != session.ConnectionId).Select(x => x.Value))
                .SendAsync("ReceivePlayerJoinedRoom");

            return new ResJoinRoom(room.Quiz?.QuizState.QuizStatus ?? QuizStatus.Starting);
        }
        else
        {
            _logger.LogError($"Wrong room password for r{room.Id}: {req.Password}");
            return Unauthorized();
        }
    }

    [HttpPost]
    [Route("StartQuiz")]
    public async Task<ActionResult> StartQuiz([FromBody] ReqStartQuiz req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == req.RoomId);

        if (room is not null)
        {
            // TODO: Check that quiz is not in the process of being started already
            if (room.Owner.Id == player.Id)
            {
                if (room.Quiz != null)
                {
                    ServerState.RemoveQuizManager(room.Quiz);
                }

                var quiz = new Quiz(room, Random.Shared.Next());
                room.Quiz = quiz;
                var quizManager = new QuizManager(quiz, _hubContext);
                ServerState.AddQuizManager(quizManager);
                room.Log("Created");

                if (await quizManager.PrimeQuiz())
                {
                    room.Log("Primed");
                    // _ = Task.Run(async () => { await quizManager.StartQuiz(); });
                    await Task.Run(async () => { await quizManager.StartQuiz(); });
                }
                else
                {
                    room.Log("Failed to prime quiz - canceling", isSystemMessage: true);
                    await quizManager.CancelQuiz();
                }
            }
            else
            {
                _logger.LogWarning("Attempt to start quiz in room {room.Id} by non-owner player {req.playerId}",
                    room.Id, req.PlayerToken);
                // todo warn not owner
            }
        }
        else
        {
            _logger.LogWarning("Attempt to start quiz in room {req.RoomId} that is null", req.RoomId);
            // todo
        }

        return Ok();
    }

    [HttpPost]
    [Route("ChangeRoomSettings")]
    public async Task<ActionResult> ChangeRoomSettings([FromBody] ReqChangeRoomSettings req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == req.RoomId);

        if (room is not null)
        {
            if (room.Owner.Id == player.Id)
            {
                if (room.Quiz is null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing)
                {
                    // room.Password = req.RoomPassword; // todo
                    room.QuizSettings = req.QuizSettings;

                    // todo syncroom in all players
                    // await _hubContext.Clients.All.SendAsync("ReceiveUpdateRoom");

                    _logger.LogInformation("Changed room settings in r{room.Id}", room.Id);
                    room.Log("Room settings changed.", isSystemMessage: true);
                    // todo write to chat (pretty)
                    return Ok();
                }
                else
                {
                    _logger.LogInformation("Cannot change room settings while quiz is active in r{room.Id}",
                        room.Id);
                    return Unauthorized();
                }
            }
            else
            {
                _logger.LogWarning("Attempt to change room settings in r{room.Id} by non-owner player", room.Id);
                return Unauthorized();
            }
        }
        else
        {
            _logger.LogWarning("Attempt to change room settings in r{req.RoomId} which is null", req.RoomId);
            return BadRequest();
        }
    }

    [HttpPost]
    [Route("SendChatMessage")]
    public async Task<ActionResult> SendChatMessage([FromBody] ReqSendChatMessage req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x =>
            x.Players.Any(y => y.Id == player.Id) || x.Spectators.Any(y => y.Id == player.Id));

        if (room is not null)
        {
            if (room.Players.Any(x => x.Id == player.Id) || room.Spectators.Any(y => y.Id == player.Id))
            {
                if (req.Contents.Length <= Constants.MaxChatMessageLength)
                {
                    var chatMessage = new ChatMessage(req.Contents, player);
                    room.Chat.Enqueue(chatMessage);
                    // todo we should only need 1 method here after a SignalR refactor
                    await _hubContext.Clients.All.SendAsync("ReceiveUpdateRoomForRoom", room);
                    await _hubContext.Clients.All.SendAsync("ReceiveUpdateRoom", room, false);
                    _logger.LogInformation($"r{room.Id} cM: {player.Username}: {req.Contents}");
                }
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to send chat message to r{req.RoomId} which p{player.Id} does not belong to",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to send chat message to a room that is null");
        }

        return Ok();
    }

    [HttpGet]
    [Route("GetServerStats")]
    public ServerStats GetServerStats()
    {
        return new ServerStats()
        {
            RoomsCount = ServerState.Rooms.Count,
            QuizManagersCount = ServerState.QuizManagers.Count,
            ActiveSessionsCount = ServerState.Sessions.Count(x => x.HasActiveConnection),
            SessionsCount = ServerState.Sessions.Count,
        };
    }
}
