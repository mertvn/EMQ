using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    [EnableRateLimiting(RateLimitKind.Login)]
    [CustomAuthorize(PermissionKind.Login)]
    [HttpPost]
    [Route("CreateSession")]
    public async Task<ActionResult<ResCreateSession>> CreateSession([FromBody] ReqCreateSession req)
    {
        string ip = ServerUtils.GetIpAddress(Request.HttpContext) ?? "";
        string username = req.Username;

        Secret? secret = null;
        User? user = null;
        if (Guid.TryParse(req.Username, out var usernameGuid)) // todo real password field check
        {
            secret = await DbManager.GetSecret(usernameGuid);
            if (secret is not null)
            {
                user = await DbManager.GetEntity_Auth<User>(secret.user_id);
                if (user != null)
                {
                    username = user.username; // todo remove
                }
                else
                {
                    throw new Exception("idk");
                }
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                user = await AuthManager.Login(req.Username, req.Password);
                if (user != null)
                {
                    secret = await AuthManager.CreateSecret(user.id, ip);
                    username = user.username; // todo remove
                }
                else
                {
                    return Unauthorized();
                }
            }
            else if (!ServerState.AllowGuests)
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

            // todo force guests to enter here regardless of username availability?
            if (!await DbManager.IsUsernameAvailable(username))
            {
                do
                {
                    username = $"Guest-{Guid.NewGuid().ToString()[..7]}";
                } while (ServerState.Sessions.Any(x => x.Player.Username == username));
            }

            do
            {
                playerId = Random.Shared.Next();
            } while (ServerState.Sessions.Any(x => x.Player.Id == playerId));
        }

        var player = new Player(playerId, username) { Avatar = new Avatar(AvatarCharacter.Auu, "default"), };
        var session = new Session(player, token, userRoleKind)
        {
            VndbInfo = new PlayerVndbInfo() { VndbId = req.VndbInfo.VndbId, Labels = req.VndbInfo.Labels }
        };

        ServerState.AddSession(session);

        _logger.LogInformation(
            $"Created new session for {session.UserRoleKind.ToString()} p{player.Id} {player.Username} ({session.VndbInfo.VndbId}) @ {ip}");

        return new ResCreateSession(session);
    }

    [EnableRateLimiting(RateLimitKind.Login)]
    [CustomAuthorize(PermissionKind.Login)]
    [HttpPost]
    [Route("RemoveSession")]
    public async Task RemoveSession([FromBody] ReqRemoveSession req)
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

        var secret = await DbManager.GetSecret(session.Player.Id);

        // enable if guest sessions are ever written to DB
        // if (secret == null)
        // {
        //     throw new Exception("idk"); // todo?
        // }

        if (secret != null)
        {
            await DbManager.DeleteEntity_Auth(secret);
        }

        ServerState.RemoveSession(session, "RemoveSession");
    }

    // todo important require user id match as well
    [EnableRateLimiting(RateLimitKind.ValidateSession)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("ValidateSession")]
    public async Task<ActionResult<Session>> ValidateSession([FromBody] Session req)
    {
        string ip = ServerUtils.GetIpAddress(Request.HttpContext) ?? "";
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.Token);
        if (session == null)
        {
            var secret = await DbManager.GetSecret(new Guid(req.Token));
            if (secret is not null)
            {
                secret = await AuthManager.RefreshSecretIfNecessary(secret, ip);

                Console.WriteLine($"Creating new session for {req.Player.Username} using previous secret");
                // Console.WriteLine($"prev vndbinfo: {JsonSerializer.Serialize(req.VndbInfo)}");
                var reqCreateSession = new ReqCreateSession(secret.token.ToString(), "", req.VndbInfo);
                session = (await CreateSession(reqCreateSession)).Value!.Session;
                // Console.WriteLine($"new vndbinfo: {JsonSerializer.Serialize(req.VndbInfo)}");
            }
            else if (ServerState.RememberGuestsBetweenServerRestarts)
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

    [EnableRateLimiting(RateLimitKind.ValidateSession)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("IsUsernameAvailable")]
    public async Task<ActionResult<bool>> IsUsernameAvailable([FromBody] string username)
    {
        return await DbManager.IsUsernameAvailable(username);
    }

    [EnableRateLimiting(RateLimitKind.Register)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("StartRegistration")]
    public async Task<ActionResult> StartRegistration(ReqStartRegistration req)
    {
        // Console.WriteLine(JsonSerializer.Serialize(req, Utils.Jso));

        bool isValid = await AuthManager.RegisterStep1SendEmail(req.Username, req.Email);
        if (!isValid)
        {
            return Unauthorized();
        }

        return Ok();
    }

    [EnableRateLimiting(RateLimitKind.Login)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("SetPassword")]
    public async Task<ActionResult<Session>> SetPassword(ReqSetPassword req)
    {
        string ip = ServerUtils.GetIpAddress(Request.HttpContext) ?? "";
        // Console.WriteLine(JsonSerializer.Serialize(req, Utils.Jso));

        int userId = await AuthManager.RegisterStep2SetPassword(req.Username, req.Token, req.NewPassword);
        if (userId <= 0)
        {
            return Unauthorized();
        }

        Secret secret = await AuthManager.CreateSecret(userId, ip);
        var reqCreateSession = new ReqCreateSession(secret.token.ToString(), "", new PlayerVndbInfo());
        var session = (await CreateSession(reqCreateSession)).Value!.Session;

        return session;
    }

    [EnableRateLimiting(RateLimitKind.Login)]
    [CustomAuthorize(PermissionKind.User)]
    [HttpPost]
    [Route("ChangePassword")]
    public async Task<ActionResult<Session>> ChangePassword(ReqChangePassword req)
    {
        string ip = ServerUtils.GetIpAddress(Request.HttpContext) ?? "";
        // Console.WriteLine(JsonSerializer.Serialize(req, Utils.Jso));

        int userId = await AuthManager.ChangePassword(req.Username, req.CurrentPassword, req.NewPassword);
        if (userId == -8) // todo hack
        {
            return StatusCode(410);
        }

        if (userId <= 0)
        {
            return Unauthorized();
        }

        Secret secret = await AuthManager.CreateSecret(userId, ip);

        // todo keep PlayerVndbInfo
        var reqCreateSession = new ReqCreateSession(secret.token.ToString(), "", new PlayerVndbInfo());
        var session = (await CreateSession(reqCreateSession)).Value!.Session;

        return session;
    }

    [EnableRateLimiting(RateLimitKind.ForgottenPassword)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("ForgottenPassword")]
    public async Task<ActionResult> ForgottenPassword(ReqForgottenPassword req)
    {
        // Console.WriteLine(JsonSerializer.Serialize(req, Utils.Jso));

        bool isValid = await AuthManager.ForgottenPasswordStep1SendEmail(req.Email);
        if (!isValid)
        {
            return Unauthorized();
        }

        return Ok();
    }

    [EnableRateLimiting(RateLimitKind.Login)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("ResetPassword")]
    public async Task<ActionResult<Session>> ResetPassword(ReqResetPassword req)
    {
        string ip = ServerUtils.GetIpAddress(Request.HttpContext) ?? "";
        // Console.WriteLine(JsonSerializer.Serialize(req, Utils.Jso));

        int userId = await AuthManager.ForgottenPasswordStep2ResetPassword(req.UserId, req.Token, req.NewPassword);
        if (userId <= 0)
        {
            return Unauthorized();
        }

        Secret secret = await AuthManager.CreateSecret(userId, ip);

        // todo keep PlayerVndbInfo
        var reqCreateSession = new ReqCreateSession(secret.token.ToString(), "", new PlayerVndbInfo());
        var session = (await CreateSession(reqCreateSession)).Value!.Session;

        return session;
    }
}
