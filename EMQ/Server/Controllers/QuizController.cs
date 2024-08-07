﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Hubs;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.Guest)]
[ApiController]
[Route("[controller]")]
public class QuizController : ControllerBase
{
    private readonly ILogger<QuizController> _logger;

    public QuizController(ILogger<QuizController> logger)
    {
        _logger = logger;
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
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

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpGet]
    [Route("SyncRoomWithTime")]
    public ResSyncRoomWithTime? SyncRoomWithTime()
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
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
        return new ResSyncRoomWithTime { Room = room, Time = DateTime.UtcNow };
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
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

    [CustomAuthorize(PermissionKind.PlayQuiz)]
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

                        string url = ClientUtils.GetPreferredSongLinkUrl(song, req.WantsVideo, req.Host)!;

                        SongHint hint = new();
                        if (room.QuizSettings.EnabledSongHintKinds.TryGetValue(SongHintKind.Msst, out bool msst) &&
                            msst)
                        {
                            hint.Sources = song.Sources;
                        }

                        if (room.QuizSettings.EnabledSongHintKinds.TryGetValue(SongHintKind.A, out bool a) && a)
                        {
                            hint.Artists = song.Artists;
                        }

                        if (room.QuizSettings.EnabledSongHintKinds.TryGetValue(SongHintKind.Mt, out bool mt) && mt)
                        {
                            hint.Titles = song.Titles;
                        }

                        return new ResNextSong(req.SongIndex, url, song.StartTime, song.ScreenshotUrl, song.CoverUrl,
                            hint);
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

    [CustomAuthorize(PermissionKind.CreateRoom)]
    [HttpPost]
    [Route("CreateRoom")]
    public async Task<ActionResult<Guid>> CreateRoom([FromBody] ReqCreateRoom req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        if (ServerState.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var owner = session.Player;
        var room = new Room(Guid.NewGuid(), req.Name, owner)
        {
            Password = req.Password, QuizSettings = req.QuizSettings
        };

        var entityRoom = new EntityRoom
        {
            id = room.Id, initial_name = room.Name, created_by = owner.Id, created_at = room.CreatedAt
        };

        await DbManager.InsertEntity(entityRoom);

        ServerState.AddRoom(room);
        _logger.LogInformation("Created room {room.Id} {room.Name} {room.Password}", room.Id, room.Name, room.Password);

        return room.Id;
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("JoinRoom")]
    public async Task<ActionResult<ResJoinRoom>> JoinRoom([FromBody] ReqJoinRoom req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session == null)
        {
            return Unauthorized();
        }

        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == req.RoomId);
        if (room is null)
        {
            _logger.LogWarning("p{player.Id} tried to join inexisting room r{room.Id}", session.Player.Id, req.RoomId);
            return BadRequest();
        }

        var player = session.Player;
        if (string.IsNullOrWhiteSpace(room.Password) || room.Password == req.Password || room.Owner == player)
        {
            if (room.Players.Any(x => x.Id == player.Id) || room.Spectators.Any(x => x.Id == player.Id))
            {
                // TODO we really shouldn't allow this (we should handle players manually changing pages better)
                return new ResJoinRoom(room.Quiz?.QuizState.QuizStatus ?? QuizStatus.Starting);
            }

            var oldRoomPlayer = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == player.Id));
            var oldRoomSpec = ServerState.Rooms.SingleOrDefault(x => x.Spectators.Any(y => y.Id == player.Id));
            if (oldRoomPlayer is not null)
            {
                _logger.LogInformation($"Removed player {player.Id} from room " + oldRoomPlayer.Id);
                oldRoomPlayer.RemovePlayer(player);
                oldRoomPlayer.Log($"{player.Username} left the room.", -1, true);

                if (!oldRoomPlayer.Players.Any())
                {
                    ServerState.RemoveRoom(oldRoomPlayer, "JoinRoom");
                }
                else
                {
                    if (oldRoomPlayer.Owner.Id == player.Id)
                    {
                        var newOwner = oldRoomPlayer.Players.First();
                        oldRoomPlayer.Owner = newOwner;
                        oldRoomPlayer.Log($"{newOwner.Username} is the new owner.", -1, true);
                    }
                }
            }
            else if (oldRoomSpec is not null)
            {
                _logger.LogInformation($"Removed spectator {player.Id} from room " + oldRoomSpec.Id);
                oldRoomSpec.RemoveSpectator(player);
                oldRoomSpec.Log($"{player.Username} left the room.", -1, true);
            }

            if (room.CanJoinDirectly)
            {
                _logger.LogInformation("Added p{player.Id} to r{room.Id}", player.Id, room.Id);
                room.Players.Enqueue(player);

                // we don't want to show this message right after room creation
                if (room.Players.Count > 1)
                {
                    room.Log($"{player.Username} joined the room.", -1, true);
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                }
            }
            else
            {
                _logger.LogInformation("Added p{player.Id} to r{room.Id} as a spectator", player.Id, room.Id);
                room.Spectators.Enqueue(player);
                room.Log($"{player.Username} started spectating.", -1, true);
            }

            // let every other player in the room know that a new player joined,
            // we can't send this message to the joining player because their room page hasn't initialized yet
            TypedQuizHub.ReceivePlayerJoinedRoom(
                room.Players.Concat(room.Spectators).Where(x => x.Id != session.Player.Id).Select(x => x.Id));

            return new ResJoinRoom(room.Quiz?.QuizState.QuizStatus ?? QuizStatus.Starting);
        }
        else
        {
            _logger.LogError($"Wrong room password for r{room.Id}: {req.Password}");
            return Unauthorized();
        }
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
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
                if (!ServerState.IsServerReadOnly)
                {
                    if (room.Quiz != null)
                    {
                        if (room.Quiz.QuizState.QuizStatus == QuizStatus.Playing)
                        {
                            return StatusCode(409);
                        }

                        ServerState.RemoveQuizManager(room.Quiz);
                    }

                    var quiz = new Quiz(room, Guid.NewGuid());
                    room.Quiz = quiz;
                    var quizManager = new QuizManager(quiz);
                    ServerState.AddQuizManager(quizManager);
                    room.Log("Created");
                    // room.Log(JsonSerializer.Serialize(room.QuizSettings, Utils.JsoIndented));

                    if (await quizManager.PrimeQuiz())
                    {
                        room.Log("Primed");
                        // ServerUtils.RunAggressiveGc();
                        await Task.Run(async () => await quizManager.StartQuiz());
                    }
                    else
                    {
                        room.Log(
                            "No songs match the current filters - canceling quiz",
                            writeToChat: true);
                        await quizManager.CancelQuiz();
                        TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id),
                            room);
                    }
                }
                else
                {
                    room.Log("Server is in read-only mode.", writeToChat: true);
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
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

    [CustomAuthorize(PermissionKind.PlayQuiz)]
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
                    var diff = QuizSettings.Diff(room.QuizSettings, req.QuizSettings);
                    // Console.WriteLine(JsonSerializer.Serialize(diff, Utils.Jso));

                    room.QuizSettings = req.QuizSettings;
                    room.Log("Room settings changed.", writeToChat: true);
                    foreach (string d in diff)
                    {
                        room.Log(d, writeToChat: true);
                    }

                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);

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

    [CustomAuthorize(PermissionKind.SendChatMessage)]
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
                    if (room.Chat.Count > 50)
                    {
                        room.Chat.TryDequeue(out _);
                    }

                    var chatMessage = new ChatMessage(req.Contents, player);
                    room.Chat.Enqueue(chatMessage);
                    // todo we should only need 1 method here after a SignalR refactor
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                    TypedQuizHub.ReceiveUpdateRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room, false);
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

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("ReturnToRoom")]
    public async Task<ActionResult> ReturnToRoom([FromBody] ReqReturnToRoom req)
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
                if (room.Quiz != null)
                {
                    var qm = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                    if (qm != null)
                    {
                        if (room.Quiz.QuizState.sp >= 0 && room.Quiz.QuizState.QuizStatus == QuizStatus.Playing)
                        {
                            room.Log($"{room.Owner.Username} used \"Return to room\".", -1, true);
                            TypedQuizHub.ReceiveUpdateRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room,
                                false);
                            await qm.EndQuiz();
                        }
                    }
                    else
                    {
                        _logger.LogError("qm not found for q{quiz.Id} in r{room.Id}",
                            room.Quiz.Id, room.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Attempt to return to room in r{room.Id} with null quiz",
                        room.Id);
                }
            }
            else
            {
                _logger.LogWarning("Attempt to return to room in r{room.Id} by non-owner p{req.playerId}",
                    room.Id, req.PlayerToken);
                // todo warn not owner
            }
        }
        else
        {
            _logger.LogWarning("Attempt to return to room in r{req.RoomId} that is null", req.RoomId);
            // todo
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpGet]
    [Route("GetRoomPassword")]
    public async Task<ActionResult<string>> GetRoomPassword(string token, Guid roomId)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == token);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == roomId);

        if (room is not null)
        {
            if (room.Owner.Id == player.Id)
            {
                return room.Password;
            }
            else
            {
                _logger.LogWarning("Attempt to get room password in r{room.Id} by non-owner p{req.playerId}",
                    room.Id, token);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to get room password in r{req.RoomId} that is null", roomId);
            // todo
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("ChangeRoomNameAndPassword")]
    public async Task<ActionResult> ChangeRoomNameAndPassword([FromBody] ReqChangeRoomNameAndPassword req)
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
                // See ReqCreateRoom
                if (req.NewName.Length > 78 || req.NewPassword.Length > 16)
                {
                    return Unauthorized();
                }

                room.Name = req.NewName;
                room.Password = req.NewPassword;

                _logger.LogInformation("Changed room name and password {room.Id} {room.Name} {room.Password}", room.Id,
                    room.Name, room.Password);
                room.Log("Room name and password changed.", -1, true);
                TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to change room name and password in r{room.Id} by non-owner p{req.playerId}",
                    room.Id, req.PlayerToken);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to change room name and password in r{req.RoomId} that is null", req.RoomId);
            // todo
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("GetRoomSongHistory")]
    public async Task<ActionResult<Dictionary<int, SongHistory>>?> GetRoomSongHistory([FromBody] Guid roomId)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == roomId);
        if (room?.Quiz is not null)
        {
            if (room.Players.Any(x => x.Id == player.Id) || room.Spectators.Any(x => x.Id == player.Id))
            {
                return room.Quiz.SongsHistory;
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to GetRoomSongHistory in r{room.Id} by non-participant p{req.playerId}",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to GetRoomSongHistory in r{req.RoomId} that is null", roomId);
        }

        return null;
    }

    // todo don't allow people to do this while playing a quiz
    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("SetTeamId")]
    public async Task<ActionResult> SetTeamId([FromBody] int requestedTeamId)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == player.Id));
        if (room is not null)
        {
            if (room.QuizSettings.TeamSize > 1)
            {
                int currentTeamSize = room.Players.Count(x => x.TeamId == requestedTeamId);
                if ((currentTeamSize + 1) <= room.QuizSettings.TeamSize)
                {
                    player.TeamId = requestedTeamId;
                    TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                    return Ok();
                }
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to set team id in r{room.Id} that is not set to teams mode by p{req.playerId}",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to set team id in r{req.RoomId} that is null", "");
        }

        return Unauthorized();
    }

    // todo don't allow people to do this while playing a quiz
    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("SetNGMCGuessesInitial")]
    public async Task<ActionResult> SetNGMCGuessesInitial([FromBody] int requestedGuesses)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == player.Id));
        if (room is not null)
        {
            if (room.QuizSettings.GamemodeKind == GamemodeKind.NGMC)
            {
                player.NGMCGuessesInitial = requestedGuesses;
                TypedQuizHub.ReceiveUpdateRoomForRoom(room.Players.Concat(room.Spectators).Select(x => x.Id), room);
                return Ok();
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to set ngmc guesses in r{room.Id} that is not set to ngmc mode by p{req.playerId}",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to set ngmc guesses in r{req.RoomId} that is null", "");
        }

        return Unauthorized();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("NGMCBurnPlayer")]
    public async Task<ActionResult> NGMCBurnPlayer([FromBody] int burnedPlayerId)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == player.Id));
        if (room is not null)
        {
            if (room.QuizSettings.GamemodeKind == GamemodeKind.NGMC && room.QuizSettings.NGMCAllowBurning)
            {
                if (room.Quiz != null)
                {
                    var qm = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                    if (qm != null)
                    {
                        var burnedPlayer = room.Players.Single(x => x.Id == burnedPlayerId);
                        await qm.NGMCBurnPlayer(burnedPlayer, player);
                        return Ok();
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to burn player in r{room.Id} that does not allow burning by p{req.playerId}",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to burn player in r{req.RoomId} that is null", "");
        }

        return Unauthorized();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("NGMCPickPlayer")]
    public async Task<ActionResult> NGMCPickPlayer([FromBody] int pickedPlayerId)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == player.Id));
        if (room is not null)
        {
            if (room.QuizSettings.GamemodeKind == GamemodeKind.NGMC)
            {
                if (room.Quiz != null)
                {
                    var qm = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                    if (qm != null)
                    {
                        var pickedPlayer = room.Players.Single(x => x.Id == pickedPlayerId);
                        await qm.NGMCPickPlayer(pickedPlayer, player, false);
                        return Ok();
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to burn player in r{room.Id} that is not set to ngmc mode by p{req.playerId}",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to burn player in r{req.RoomId} that is null", "");
        }

        return Unauthorized();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("NGMCDontBurn")]
    public async Task<ActionResult> NGMCDontBurn()
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var player = session.Player;
        var room = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == player.Id));
        if (room is not null)
        {
            if (room.QuizSettings.GamemodeKind == GamemodeKind.NGMC && room.QuizSettings.NGMCAllowBurning)
            {
                if (room.Quiz != null)
                {
                    var qm = ServerState.QuizManagers.SingleOrDefault(x => x.Quiz.Id == room.Quiz.Id);
                    if (qm != null)
                    {
                        await qm.NGMCDontBurn(player);
                        return Ok();
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "Attempt to burn player in r{room.Id} that does not allow burning by p{req.playerId}",
                    room.Id, player.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempt to burn player in r{req.RoomId} that is null", "");
        }

        return Unauthorized();
    }

    [CustomAuthorize(PermissionKind.PlayQuiz)]
    [HttpPost]
    [Route("GetSHRoomContainers")]
    public async Task<ActionResult<List<SHRoomContainer>>> GetSHRoomContainers(ReqGetSHRoomContainers req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var shRoomContainers = await DbManager.GetSHRoomContainers(req.UserId, req.StartDate, req.EndDate);
        return shRoomContainers;
    }
}
