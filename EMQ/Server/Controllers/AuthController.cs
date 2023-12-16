using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.Visitor)]
[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<AuthController> _logger;

    [CustomAuthorize(PermissionKind.Login)]
    [HttpPost]
    [Route("CreateSession")]
    public async Task<ActionResult<ResCreateSession>> CreateSession([FromBody] ReqCreateSession req)
    {
        string username = req.Username;

        Secret? secret = null;
        User? user = null;
        if (Guid.TryParse(req.Username, out var usernameGuid)) // todo real password check
        {
            secret = await DbManager.GetSecret(usernameGuid);
            if (secret is not null)
            {
                secret = await RefreshSessionIfNecessary(secret);

                user = await DbManager.GetUser(secret.user_id);
                if (user != null)
                {
                    username = user.username;
                }
                else
                {
                    throw new Exception("idk");
                }
            }
        }
        else
        {
            if (!ServerState.AllowGuests)
            {
                return Unauthorized();
            }
        }

        int playerId;
        string token;
        UserRoleKind userRoleKind;
        if (secret is not null)
        {
            token = secret.token.ToString();
            userRoleKind = (UserRoleKind)user.roles;
            playerId = user.id;

            var existingSession = ServerState.Sessions.SingleOrDefault(x => x.Player.Id == playerId);
            if (existingSession != null)
            {
                // todo db
                ServerState.RemoveSession(existingSession, "CreateSession");
            }
        }
        else
        {
            // todo require explicitly clicking on Play as Guest to get here
            token = Guid.NewGuid().ToString();
            bool isGuest = false; // todo
            if (isGuest)
            {
                userRoleKind = UserRoleKind.Guest;
            }
            else
            {
                userRoleKind = UserRoleKind.User;
            }

            do
            {
                playerId = Random.Shared.Next();
            } while (ServerState.Sessions.Any(x => x.Player.Id == playerId));

            // secret = new Secret
            // {
            //     username = username,
            //     roles = (int)userRoleKind,
            //     token = new Guid(token),
            //     created_at = DateTime.UtcNow
            // };
            //
            // var userId = await DbManager.InsertSecret(secret);
            // if (userId <= 0)
            // {
            //     throw new Exception("idk"); // todo?
            // }
            //
            // playerId = userId;
        }

        var player = new Player(playerId, username) { Avatar = new Avatar(AvatarCharacter.Auu, "default"), };
        var session = new Session(player, token, userRoleKind)
        {
            VndbInfo = new PlayerVndbInfo() { VndbId = req.VndbInfo.VndbId, Labels = req.VndbInfo.Labels }
        };

        ServerState.AddSession(session);

        string? ip = ServerUtils.GetIpAddress(Request.HttpContext);
        _logger.LogInformation(
            $"Created new session for {session.UserRoleKind.ToString()} p{player.Id} {player.Username} ({session.VndbInfo.VndbId}) @ {ip}");

        return new ResCreateSession(session);
    }

    // todo move
    private static async Task<Secret> RefreshSessionIfNecessary(Secret secret)
    {
        return secret;

        // todo do lastUsed instead of this?
        TimeSpan maxAge = TimeSpan.FromDays(30);
        Console.WriteLine(
            $"time diff: {(DateTime.UtcNow - secret.created_at).TotalSeconds.ToString(CultureInfo.InvariantCulture)}");
        if (DateTime.UtcNow - secret.created_at > maxAge)
        {
            Console.WriteLine($"refreshing session for {secret.user_id}");
            var token = Guid.NewGuid();
            secret.token = token;
            secret.created_at = DateTime.UtcNow;

            if (await DbManager.UpdateSecret(secret))
            {
                return secret;
            }
            else
            {
                throw new Exception("idk"); // todo?
            }
        }
        else
        {
            return secret;
        }
    }

    [CustomAuthorize(PermissionKind.Login)]
    [HttpPost]
    [Route("RemoveSession")]
    public void RemoveSession([FromBody] ReqRemoveSession req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.Token);
        _logger.LogInformation("Removing session " + session?.Token);
        if (session == null)
        {
            return;
        }

        var room = ServerState.Rooms.FirstOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
        if (room != null)
        {
            room.RemovePlayer(session.Player);
            room.AllConnectionIds.Remove(session.Player.Id, out _);
            if (!room.Players.Any())
            {
                ServerState.RemoveRoom(room, "RemoveSession");
            }
        }

        // todo db stuff
        ServerState.RemoveSession(session, "RemoveSession");
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("ValidateSession")]
    public async Task<ActionResult<Session>> ValidateSession([FromBody] Session req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.Token);
        if (session == null)
        {
            var secret = await DbManager.GetSecret(new Guid(req.Token));
            if (secret is not null)
            {
                secret = await RefreshSessionIfNecessary(secret);

                Console.WriteLine($"Creating new session for {req.Player.Username} using previous secret");
                // Console.WriteLine($"prev vndbinfo: {JsonSerializer.Serialize(req.VndbInfo)}");
                var reqCreateSession = new ReqCreateSession(secret.token.ToString(), "", req.VndbInfo);
                session = (await CreateSession(reqCreateSession)).Value!.Session;
                // Console.WriteLine($"new vndbinfo: {JsonSerializer.Serialize(req.VndbInfo)}");
            }
            else if (ServerState.RememberGuests)
            {
                Console.WriteLine($"Creating new session for {req.Player.Username} using previous session");
                // Console.WriteLine($"prev vndbinfo: {JsonSerializer.Serialize(req.VndbInfo)}");
                var reqCreateSession = new ReqCreateSession(req.Player.Username, "", req.VndbInfo);
                session = (await CreateSession(reqCreateSession)).Value!.Session;
                // Console.WriteLine($"new vndbinfo: {JsonSerializer.Serialize(req.VndbInfo)}");
            }
        }

        return session == null ? Unauthorized() : session;
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("UpdateLabel")]
    public async Task<ActionResult<Label>> UpdateLabel([FromBody] ReqUpdateLabel req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session == null)
        {
            return Unauthorized();
        }

        if (session.VndbInfo.Labels != null)
        {
            var existingLabel = session.VndbInfo.Labels.FirstOrDefault(x => x.Id == req.Label.Id);
            Console.WriteLine(
                $"{session.VndbInfo.VndbId}: {existingLabel?.Id ?? req.Label.Id} ({existingLabel?.Name ?? req.Label.Name}), {existingLabel?.Kind ?? 0} => {req.Label.Kind}");

            if (existingLabel != null)
            {
                session.VndbInfo.Labels.RemoveAll(x => x.Id == req.Label.Id);
            }

            session.VndbInfo.Labels.Add(req.Label);
            return req.Label;
        }
        else
        {
            throw new Exception("Could not find the label to update");
        }
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("UpdatePlayerPreferences")]
    public async Task<ActionResult<PlayerPreferences>> UpdatePlayerPreferences(
        [FromBody] ReqUpdatePlayerPreferences req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session == null)
        {
            return Unauthorized();
        }

        session.Player.Preferences = req.PlayerPreferences;
        return session.Player.Preferences;
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("SetVndbInfo")]
    public async Task<ActionResult> SetVndbInfo([FromBody] ReqSetVndbInfo req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session == null)
        {
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(req.VndbInfo.VndbId))
        {
            _logger.LogInformation($"SetVndbInfo for p{session.Player.Id} to {req.VndbInfo.VndbId}");
            session.VndbInfo = req.VndbInfo;
            return Ok();
        }
        else
        {
            return BadRequest();
        }
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("CspReport")]
    public async Task<IActionResult> CspReport([FromBody] dynamic report)
    {
        string serialized = (string)JsonSerializer.Serialize(report, Utils.JsoIndented);
        if (!serialized.Contains("blazor.webassembly.js") &&
            !serialized.Contains("moz-extension") &&
            !serialized.Contains("chrome-extension") &&
            !serialized.Contains("google-analytics"))
        {
            _logger.LogError("CSP violation: " + serialized);
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpGet]
    [Route("GetServerStats")]
    public ServerStats GetServerStats()
    {
        return new ServerStats()
        {
            RoomsCount = ServerState.Rooms.Count,
            QuizManagersCount = ServerState.QuizManagers.Count,
            ActiveSessionsCount = ServerState.Sessions.Count(x => x.Player.HasActiveConnection),
            SessionsCount = ServerState.Sessions.Count,
        };
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpGet]
    [Route("GetRooms")]
    public IEnumerable<Room> GetRooms()
    {
        var ret = ServerState.Rooms.ToList();

        ret = JsonSerializer.Deserialize<List<Room>>(JsonSerializer.Serialize(ret))!; // need deep-copy
        foreach (Room room in ret)
        {
            room.Chat = null!;
        }

        return ret;
    }
}
