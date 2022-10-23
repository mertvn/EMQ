using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorApp1.Server.Business;
using BlazorApp1.Server.Hubs;
using BlazorApp1.Shared;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Quiz;
using BlazorApp1.Shared.Quiz.Entities.Concrete;
using BlazorApp1.Shared.Quiz.Entities.Concrete.Dto.Request;
using BlazorApp1.Shared.Quiz.Entities.Concrete.Dto.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlazorApp1.Server.Controllers;

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

    [HttpPost]
    [Route("SyncRoom")]
    public Room? SyncRoom([FromBody] int roomId)
    {
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == roomId);
        if (room is null)
        {
            _logger.LogError("Room not found: " + roomId);
            return null;
        }

        return room;
    }

    [HttpPost]
    [Route("NextSong")]
    public ResNextSong? NextSong([FromBody] ReqNextSong req)
    {
        // todo? verify user belongs in room
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == req.RoomId);

        if (room is not null)
        {
            if (room.Quiz != null)
            {
                if (req.SongIndex <= room.Quiz.QuizState.sp + room.Quiz.QuizSettings.PreloadAmount)
                {
                    if (req.SongIndex < room.Quiz.Songs.Count)
                    {
                        string url = room.Quiz.Songs[req.SongIndex].Url;
                        return new ResNextSong(req.SongIndex, url);
                    }
                    else
                    {
                        _logger.LogError("Requested song index is invalid: " + req.SongIndex);
                        return null;
                    }
                }
                else
                {
                    _logger.LogError("Requested song index is too far in the future: " + req.SongIndex);
                    return null;
                }
            }
            else
            {
                _logger.LogError("Room does not have a quiz initialized: " + req.RoomId);
                return null;
            }
        }
        else
        {
            _logger.LogError("Room not found: " + req.RoomId);
            return null;
        }
    }

    [HttpGet]
    [Route("GetRooms")]
    public async Task<IEnumerable<Room>> GetRooms()
    {
        return ServerState.Rooms; // todo: only return "active" rooms?
    }

    [HttpPost]
    [Route("CreateRoom")]
    public async Task<int> CreateRoom([FromBody] ReqCreateRoom req)
    {
        var session = ServerState.Sessions.Find(x => x.Player.Id == req.PlayerId);
        if (session is null)
        {
            // todo
            throw new Exception();
        }

        var owner = session.Player;
        var room = new Room(new Random().Next(), req.Name, owner) { Password = req.Password, };

        var quiz = new Quiz(req.QuizSettings, room);

        room.Quiz = quiz;

        // todo
        ServerState.Rooms.Add(room);
        ServerState.QuizManagers.Add(new QuizManager(quiz, _hubContext));

        _logger.LogInformation("Created room " + room.Id);
        _logger.LogInformation("Created quiz " + quiz.Guid);
        return room.Id;
    }

    [HttpPost]
    [Route("JoinRoom")]
    public async Task JoinRoom([FromBody] ReqJoinRoom req)
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
            // todo check if player is already in the same room?
            if (!room.Players.Any(x => x.Id == req.PlayerId))
            {
                var oldRoom = ServerState.Rooms.Find(x => x.Players.Any(y => y.Id == req.PlayerId));
                if (oldRoom is not null)
                {
                    _logger.LogInformation($"Removed player {req.PlayerId} from room " + room.Id);
                    oldRoom.Players.RemoveAll(x => x.Id == player.Id);
                }

                _logger.LogInformation($"Added player {req.PlayerId} to room " + room.Id);
                room.Players.Add(player);
                // _logger.LogInformation("cnnid: " + session.ConnectionId!);
                room.AllPlayerConnectionIds.Add(session.ConnectionId!);

                // let every other player in the room know that a new player joined,
                // we can't send this message to the joining player because their room page hasn't initialized yet
                await _hubContext.Clients.Clients(room.AllPlayerConnectionIds.Where(x => x != session.ConnectionId))
                    .SendAsync("ReceivePlayerJoinedRoom");
            }
            else
            {
                // todo warn error
            }
        }
        else
        {
            // todo warn wrong password
        }
    }

    [HttpPost]
    [Route("StartQuiz")]
    public async Task StartQuiz([FromBody] ReqStartQuiz req)
    {
        var session = ServerState.Sessions.Find(x => x.Player.Id == req.PlayerId);
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
                var quiz = room.Quiz!;

                var quizManager = ServerState.QuizManagers.Find(x => x.Quiz.Guid == quiz.Guid);
                if (quizManager is not null)
                {
                    // todo
                    quiz.Songs = new List<Song>
                    {
                        new() { Name = "burst", Url = "https://files.catbox.moe/b4b5wl.mp3", Data = "" },
                        new() { Name = "shuffle", Url = "https://files.catbox.moe/8sxb1b.webm", Data = "" },
                        new() { Name = "inukami", Url = "https://files.catbox.moe/kk3ndn.mp3", Data = "" },
                        new() { Name = "gintama", Url = "https://files.catbox.moe/ftvkr9.mp3", Data = "" },
                        new() { Name = "lovehina", Url = "https://files.catbox.moe/pwuc9j.mp3", Data = "" },
                        new() { Name = "akunohana", Url = "https://files.catbox.moe/dupkk6.webm", Data = "" },
                        new() { Name = "fsn", Url = "https://files.catbox.moe/d1boaz.webm", Data = "" },
                        new() { Name = "h2o", Url = "https://files.catbox.moe/tf82bf.webm", Data = "" },
                        new() { Name = "chihayafuru", Url = " https://files.catbox.moe/y3ps2h.mp3", Data = "" },
                    };

                    quiz.Songs = quiz.Songs.OrderBy(a => new Random().Next()).ToList(); // todo
                    quiz.QuizState.NumSongs = quiz.Songs.Count;


                    await quizManager.StartQuiz();
                    _logger.LogInformation("Started quiz " + quiz.Guid);
                }
                else
                {
                    // todo
                }
            }
            else
            {
                // todo warn not owner
            }
        }
        else
        {
            // todo
        }
    }
}
