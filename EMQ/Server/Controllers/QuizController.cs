using System;
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
        var session = ServerState.Sessions.Single(x => x.Token == token);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
        if (room is null)
        {
            _logger.LogError("Room not found with playerToken: " + token);
            return null;
        }

        // _logger.LogError(JsonSerializer.Serialize(room));
        return room;
    }

    [HttpPost]
    [Route("NextSong")]
    public ActionResult<ResNextSong> NextSong([FromBody] ReqNextSong req)
    {
        var session = ServerState.Sessions.Single(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
        if (room is not null)
        {
            if (room.Quiz != null)
            {
                if (req.SongIndex <= room.Quiz.QuizState.sp + room.Quiz.Room.QuizSettings.PreloadAmount)
                {
                    if (req.SongIndex < room.Quiz.Songs.Count)
                    {
                        var song = room.Quiz.Songs[req.SongIndex];
                        string url = song.Links.First().Url; // todo link selection
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
        // todo
        // return ServerState.Rooms.Where(x => x.Quiz is null ||
        //                                     x.Quiz?.QuizState.QuizStatus is QuizStatus.Starting or QuizStatus.Playing);

        return ServerState.Rooms;
    }

    [HttpPost]
    [Route("CreateRoom")]
    public int CreateRoom([FromBody] ReqCreateRoom req)
    {
        var session = ServerState.Sessions.Single(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var owner = session.Player;
        var room = new Room(new Random().Next(), req.Name, owner)
        {
            Password = req.Password, QuizSettings = req.QuizSettings
        };
        ServerState.Rooms.Add(room);
        _logger.LogInformation("Created room " + room.Id);

        return room.Id;
    }

    // todo decouple joining room from joining quiz maybe?
    [HttpPost]
    [Route("JoinRoom")]
    public async Task<ResJoinRoom> JoinRoom([FromBody] ReqJoinRoom req)
    {
        var room = ServerState.Rooms.Find(x => x.Id == req.RoomId);
        var session = ServerState.Sessions.Find(x => x.Player.Id == req.PlayerId);

        if (room is null || session is null)
        {
            // todo warn error
            throw new Exception();
        }

        var player = session.Player;
        if (room.Password == req.Password)
        {
            if (room.Players.Any(x => x.Id == req.PlayerId))
            {
                // TODO probably shouldn't allow this after the necessary changes for detecting players leaving rooms is completed
                return new ResJoinRoom(0);
            }

            var oldRoom = ServerState.Rooms.Find(x => x.Players.Any(y => y.Id == req.PlayerId));
            if (oldRoom is not null)
            {
                _logger.LogInformation($"Removed player {req.PlayerId} from room " + oldRoom.Id);
                oldRoom.Players.RemoveAll(x => x.Id == player.Id);
            }

            // hotjoins have to be handled differently
            if (room.Quiz?.QuizState.Phase is QuizPhaseKind.Guess or QuizPhaseKind.Judgement
                or QuizPhaseKind.Looting)
            {
                if (!room.Quiz.JoinQueue.Any(x => x.Player.Id == req.PlayerId))
                {
                    // todo quizlog
                    room.Quiz.Log("Added player to JoinQueue", player.Id);
                    room.Quiz.JoinQueue.Enqueue(session);

                    return new ResJoinRoom((int)(room.Quiz.QuizState.RemainingMs + 5));
                }
                else
                {
                    // todo
                    throw new Exception();
                }
            }
            else if (room.Quiz is null || room.Quiz.QuizState.Phase == QuizPhaseKind.Results)
            {
                _logger.LogInformation($"Added player {req.PlayerId} to room " + room.Id);
                room.Players.Add(player);
                // _logger.LogInformation("cnnid: " + session.ConnectionId!);
                room.AllPlayerConnectionIds[player.Id] = session.ConnectionId!;

                // let every other player in the room know that a new player joined,
                // we can't send this message to the joining player because their room page hasn't initialized yet
                await _hubContext.Clients.Clients(room.AllPlayerConnectionIds
                        .Where(x => x.Value != session.ConnectionId).Select(x => x.Value))
                    .SendAsync("ReceivePlayerJoinedRoom");

                return new ResJoinRoom(0);
            }
            else
            {
                _logger.LogError("Invalid room/quiz state when attempting to add new player to room");
                throw new Exception("Invalid room/quiz state when attempting to add new player to room");
            }
        }
        else
        {
            // todo warn wrong password
            throw new Exception();
        }
    }

    [HttpPost]
    [Route("StartQuiz")]
    public async Task StartQuiz([FromBody] ReqStartQuiz req)
    {
        var session = ServerState.Sessions.Find(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var player = session.Player;
        var room = ServerState.Rooms.Find(x => x.Id == req.RoomId);

        if (room is not null)
        {
            if (room.Owner.Id == player.Id)
            {
                var quiz = new Quiz(room, new Random().Next());
                room.Quiz = quiz;
                var quizManager = new QuizManager(quiz, _hubContext);
                ServerState.QuizManagers.Add(quizManager);
                quiz.Log("Created");

                if (await quizManager.PrimeQuiz())
                {
                    quiz.Log("Primed");
                    await quizManager.StartQuiz();
                }
                else
                {
                    quiz.Log("Failed to prime - canceling");
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
    }

    [HttpPost]
    [Route("ChangeRoomSettings")]
    public void ChangeRoomSettings([FromBody] ReqChangeRoomSettings req)
    {
        var session = ServerState.Sessions.Find(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var player = session.Player;
        var room = ServerState.Rooms.Find(x => x.Id == req.RoomId);

        if (room is not null)
        {
            if (room.Owner.Id == player.Id)
            {
                if (room.Quiz is null || room.Quiz.QuizState.QuizStatus != QuizStatus.Playing)
                {
                    room.Password = req.RoomPassword;
                    room.QuizSettings = req.QuizSettings;

                    _logger.LogInformation("Changed room settings in r{room.Id}", room.Id);
                    room.Quiz?.Log("Changed room settings");
                    // todo write to chat
                }
                else
                {
                    _logger.LogInformation("Cannot change room settings while quiz is active in r{room.Id}",
                        room.Id);
                }
            }
            else
            {
                _logger.LogWarning("Attempt to change room settings in r{room.Id} by non-owner player", room.Id);
                // todo warn not owner
            }
        }
        else
        {
            _logger.LogWarning("Attempt to change room settings in r{req.RoomId} which is null", req.RoomId);
            // todo
        }
    }

    [HttpPost]
    [Route("SendChatMessage")]
    public void SendChatMessage([FromBody] ReqSendChatMessage req)
    {
        var session = ServerState.Sessions.Find(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var player = session.Player;
        var room = ServerState.Rooms.Find(x => x.Players.Any(y => y.Id == player.Id));

        if (room is not null)
        {
            if (room.Players.Any(x => x.Id == player.Id))
            {
                if (req.Contents.Length <= Constants.MaxChatMessageLength)
                {
                    var chatMessage = new ChatMessage(req.Contents, player);
                    room.Chat.Enqueue(chatMessage);
                    _logger.LogInformation($"r{room.Id} cM: {player.Username}: {req.Contents}");
                    // todo sync room for all players
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
            _logger.LogWarning("Attempt to send chat message to r{req.RoomId} which is null", room.Id);
        }
    }
}
