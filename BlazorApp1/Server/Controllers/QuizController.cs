using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorApp1.Server.Hubs;
using BlazorApp1.Server.Model;
using BlazorApp1.Shared;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Quiz;
using BlazorApp1.Shared.Quiz.Dto.Request;
using BlazorApp1.Shared.Quiz.Dto.Response;
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

    public static readonly List<Room> Rooms = new() { };

    [HttpPost]
    [Route("SyncQuizState")]
    public QuizState? SyncQuizState([FromBody] int roomId)
    {
        var room = Rooms.SingleOrDefault(x => x.Id == roomId);
        if (room is null)
        {
            _logger.LogError("Room not found: " + roomId);
            return null;
        }

        var quiz = room.Quiz;
        if (quiz is null)
        {
            _logger.LogError("Room does not have a quiz initialized: " + roomId);
            return null;
        }

        return quiz.QuizState;
    }

    [HttpPost]
    [Route("NextSong")]
    public ResNextSong NextSong([FromBody] ReqNextSong request)
    {
        // todo? verify user belongs in room
        // todo check that request.SongIndex is less than sp+preloadAmount
        var room = Rooms.SingleOrDefault(x => x.Id == request.RoomId);

        // todo
        // if (room is not null)
        // {
        var url = room.Quiz.Songs[request.SongIndex].Url;
        return new ResNextSong(request.SongIndex, url);
        // }
    }

    [HttpPost]
    [Route("CreateRoom")]
    public async Task<int> CreateRoom([FromBody] ReqCreateRoom request)
    {
        // todo
        var room = new Room
        {
            Id = new Random().Next(),
            Name = request.Name,
            Password = request.Password,
            Quiz = new Quiz(_hubContext, request.QuizSettings)
            {
                Songs = new List<Song>()
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
                }
            }
        };

        // todo
        room.Quiz.Songs = room.Quiz.Songs.OrderBy(a => new Random().Next()).ToList();
        Rooms.Add(room);

        return room.Id;
    }

    [HttpPost]
    [Route("JoinRoom")]
    public async Task JoinRoom([FromBody] ReqJoinRoom request)
    {
        var room = Rooms.Find(x => x.Id == request.RoomId);
        if (room.Password == request.Password)
        {
            if (!room.Quiz.Players.Any(x => x.Id == request.PlayerId))
            {
                room.Quiz.Players.Add(new Player() { Username = "TestPlayer", Id = request.PlayerId }); // todo
            }
        }
    }
}
