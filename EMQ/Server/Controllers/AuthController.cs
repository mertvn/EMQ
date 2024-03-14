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
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

// todo? require userid + token everywhere instead of just token?
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

        string username;
        int playerId;
        string token;
        UserRoleKind userRoleKind;

        if (req.IsGuest)
        {
            if (!ServerState.AllowGuests)
            {
                return Unauthorized();
            }

            token = Guid.NewGuid().ToString();
            userRoleKind = UserRoleKind.Guest;

            do
            {
                int random;
                do
                {
                    random = Random.Shared.Next();
                } while (random < 1_000_000);

                playerId = Convert.ToInt32(random.ToString()[..7]);
            } while (ServerState.Sessions.Any(x => x.Value.Player.Id == playerId));

            username = $"Guest-{playerId}";
        }
        else if (!string.IsNullOrWhiteSpace(req.Password))
        {
            User? user = await AuthManager.Login(req.UsernameOrEmail, req.Password);
            if (user != null)
            {
                Secret secret = await AuthManager.CreateSecret(user.id, ip);
                username = user.username;
                token = secret.token.ToString();
                userRoleKind = (UserRoleKind)user.roles;
                playerId = user.id;

                var existingSession = ServerState.Sessions.SingleOrNull(x => x.Value.Player.Id == playerId)?.Value;
                if (existingSession != null)
                {
                    // todo db (if necessary in the future)
                    ServerState.RemoveSession(existingSession, "CreateSession");
                }
            }
            else
            {
                return Unauthorized();
            }
        }
        else if (!string.IsNullOrWhiteSpace(req.Token))
        {
            var secret = await DbManager.GetSecret(req.UserId, new Guid(req.Token));
            if (secret is not null)
            {
                var user = await DbManager.GetEntity_Auth<User>(secret.user_id);
                if (user != null)
                {
                    username = user.username;
                    token = secret.token.ToString();
                    userRoleKind = (UserRoleKind)user.roles;
                    playerId = user.id;

                    var existingSession = ServerState.Sessions.SingleOrNull(x => x.Value.Player.Id == playerId)?.Value;
                    if (existingSession != null)
                    {
                        // todo db (if necessary in the future)
                        ServerState.RemoveSession(existingSession, "CreateSession");
                    }
                }
                else
                {
                    throw new Exception("Secret without user");
                }
            }
            else
            {
                return Unauthorized();
            }
        }
        else
        {
            return Unauthorized();
        }

        var vndbInfo = await ServerUtils.GetVndbInfo_Inner(playerId);
        var player = new Player(playerId, username) { Avatar = new Avatar(AvatarCharacter.Auu, "default"), };
        var session = new Session(player, token, userRoleKind);

        ServerState.AddSession(session);

        _logger.LogInformation(
            $"Created new session for {session.UserRoleKind.ToString()} p{player.Id} {player.Username} ({vndbInfo.VndbId}) @ {ip}");

        return new ResCreateSession(session, vndbInfo);
    }

    [EnableRateLimiting(RateLimitKind.ValidateSession)]
    [CustomAuthorize(PermissionKind.Login)]
    [HttpPost]
    [Route("RemoveSession")]
    public async Task RemoveSession([FromBody] ReqRemoveSession req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        _logger.LogInformation("Removing session " + session?.Token);
        if (session == null)
        {
            return;
        }

        var player = session.Player;
        var oldRoomPlayer = ServerState.Rooms.SingleOrNull(x => x.Value.Players.Any(y => y.Value.Id == player.Id))
            ?.Value;
        var oldRoomSpec = ServerState.Rooms.SingleOrNull(x => x.Value.Spectators.Any(y => y.Value.Id == player.Id))
            ?.Value;
        if (oldRoomPlayer is not null)
        {
            oldRoomPlayer.RemovePlayer(player);
            oldRoomPlayer.AllConnectionIds.Remove(player.Id, out _);
            oldRoomPlayer.Log($"{player.Username} left the room.", -1, true);

            // todo this doesnt work correctly sometimes idk
            if (!oldRoomPlayer.Players.Any())
            {
                ServerState.RemoveRoom(oldRoomPlayer, "RemoveSession");
            }
        }
        else if (oldRoomSpec is not null)
        {
            oldRoomSpec.RemoveSpectator(player);
            oldRoomSpec.AllConnectionIds.Remove(player.Id, out _);
            oldRoomSpec.Log($"{player.Username} left the room.", -1, true);

            if (!oldRoomSpec.Players.Any())
            {
                ServerState.RemoveRoom(oldRoomSpec, "RemoveSession");
            }
        }

        var secret = await DbManager.GetSecret(session.Player.Id, new Guid(session.Token));

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

    [EnableRateLimiting(RateLimitKind.ValidateSession)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("ValidateSession")]
    public async Task<ActionResult<ResValidateSession>> ValidateSession([FromBody] Session req)
    {
        string ip = ServerUtils.GetIpAddress(Request.HttpContext) ?? "";

        // for this endpoint only, we expect the token in the request body instead of the Authorization header
        var session = ServerState.Sessions.SingleOrNull(x => x.Value.Token == req.Token)?.Value;

        var secret = await DbManager.GetSecret(req.Player.Id, new Guid(req.Token));
        if (secret is not null)
        {
            secret = await AuthManager.RefreshSecretIfNecessary(secret, ip);
            if (session != null)
            {
                session.Token = secret.token.ToString();
            }
        }

        PlayerVndbInfo? vndbInfo = null;
        if (session == null)
        {
            if (secret is not null)
            {
                Console.WriteLine($"Creating new session for {req.Player.Username} using previous secret");
                var reqCreateSession = new ReqCreateSession(secret.user_id, secret.token.ToString());
                var res = (await CreateSession(reqCreateSession)).Value!;
                session = res.Session;
                vndbInfo = res.VndbInfo;
            }
            else if (ServerState.RememberGuestsBetweenServerRestarts)
            {
                Console.WriteLine($"Creating new session for {req.Player.Username} using previous session");
                var reqCreateSession = new ReqCreateSession(req.Player.Username, "", true);
                var res = (await CreateSession(reqCreateSession)).Value!;
                session = res.Session;
                vndbInfo = res.VndbInfo;
            }
        }

        if (session == null)
        {
            return Unauthorized();
        }
        else
        {
            vndbInfo ??= await ServerUtils.GetVndbInfo_Inner(session.Player.Id);
            return new ResValidateSession(session, vndbInfo);
        }
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("UpdateLabel")]
    public async Task<ActionResult<Label>> UpdateLabel([FromBody] ReqUpdateLabel req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        PlayerVndbInfo vndbInfo = await DbManager.GetUserVndbInfo(session.Player.Id);
        if (string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            throw new Exception($"Couldn't GetUserVndbInfo for p{session.Player.Id}");
        }

        // todo
        var userLabel = new UserLabel
        {
            user_id = session.Player.Id,
            vndb_uid = vndbInfo.VndbId,
            vndb_label_id = req.Label.Id,
            vndb_label_name = req.Label.Name,
            vndb_label_is_private = req.Label.IsPrivate,
            kind = (int)req.Label.Kind,
        };
        long userLabelId = await DbManager.RecreateUserLabel(userLabel, req.Label.VNs);

        var userLabelVns = await DbManager.GetUserLabelVns(userLabelId);
        var label = ServerUtils.FromUserLabel(userLabel);
        label.VNs = userLabelVns.ToDictionary(x => x.vnid, x => x.vote);

        return label;
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("UpdatePlayerPreferences")]
    public async Task<ActionResult<PlayerPreferences>> UpdatePlayerPreferences(
        [FromBody] ReqUpdatePlayerPreferences req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        // todo? db
        session.Player.Preferences = req.PlayerPreferences;
        return session.Player.Preferences;
    }

    // [CustomAuthorize(PermissionKind.UpdatePreferences)]
    // [HttpPost]
    // [Route("GetVndbInfo")]
    // public async Task<ActionResult<PlayerVndbInfo>> GetVndbInfo([FromBody] ReqSetVndbInfo req)
    // {
    //     var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
    //     if (session == null)
    //     {
    //         return Unauthorized();
    //     }
    //
    //     var vndbInfo = await ServerUtils.GetVndbInfo_Inner(session.Player.Id);
    //     return vndbInfo;
    // }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("SetVndbInfo")]
    public async Task<ActionResult<PlayerVndbInfo>> SetVndbInfo([FromBody] ReqSetVndbInfo req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        _logger.LogInformation($"SetVndbInfo for p{session.Player.Id} to {req.VndbInfo.VndbId}");

        // Constraint: A user can only have a single vndb account connected at any given time
        await DbManager.DeleteUserLabels(session.Player.Id);

        if (!string.IsNullOrWhiteSpace(req.VndbInfo.VndbId) && req.VndbInfo.Labels is not null)
        {
            // todo? batch
            foreach (Label label in req.VndbInfo.Labels)
            {
                var userLabel = new UserLabel
                {
                    user_id = session.Player.Id,
                    vndb_uid = req.VndbInfo.VndbId,
                    vndb_label_id = label.Id,
                    vndb_label_name = label.Name,
                    vndb_label_is_private = label.IsPrivate,
                    kind = (int)label.Kind,
                };
                long _ = await DbManager.RecreateUserLabel(userLabel, label.VNs);
            }
        }

        // todo this is inefficient
        var vndbInfo = await ServerUtils.GetVndbInfo_Inner(session.Player.Id);
        return vndbInfo;
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
            ActiveSessionsCount = ServerState.Sessions.Count(x => x.Value.Player.HasActiveConnection),
            SessionsCount = ServerState.Sessions.Count,
            IsServerReadOnly = ServerState.IsServerReadOnly,
            IsSubmissionDisabled = ServerState.IsSubmissionDisabled,
            GitHash = ServerState.GitHash,
        };
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpGet]
    [Route("GetRooms")]
    public IEnumerable<Room> GetRooms()
    {
        var ret = ServerState.Rooms.Values.ToList();

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
        var reqCreateSession = new ReqCreateSession(userId, secret.token.ToString());
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

        var reqCreateSession = new ReqCreateSession(userId, secret.token.ToString());
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

        var reqCreateSession = new ReqCreateSession(userId, secret.token.ToString());
        var session = (await CreateSession(reqCreateSession)).Value!.Session;

        return session;
    }

    [CustomAuthorize(PermissionKind.StoreQuizSettings)]
    [HttpGet]
    [Route("GetUserQuizSettings")]
    public async Task<ActionResult<List<ResGetUserQuizSettings>>> GetUserQuizSettings(string token)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        return await DbManager.SelectUserQuizSettings(session.Player.Id);
    }

    [CustomAuthorize(PermissionKind.StoreQuizSettings)]
    [HttpPost]
    [Route("StoreUserQuizSettings")]
    public async Task<ActionResult> StoreUserQuizSettings([FromBody] ReqStoreUserQuizSettings req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await DbManager.InsertUserQuizSettings(session.Player.Id, req.Name, req.B64);
        Console.WriteLine($"p{session.Player.Id} {session.Player.Username} saved preset {req.Name} {req.B64.Length}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.StoreQuizSettings)]
    [HttpPost]
    [Route("DeleteUserQuizSettings")]
    public async Task<ActionResult> DeleteUserQuizSettings([FromBody] ReqDeleteUserQuizSettings req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await DbManager.DeleteUserQuizSettings(session.Player.Id, req.Name);
        Console.WriteLine($"p{session.Player.Id} {session.Player.Username} deleted preset {req.Name}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetPublicUserInfo")]
    public async Task<ActionResult<ResGetPublicUserInfo>> GetPublicUserInfo([FromBody] int userId)
    {
        var publicUserInfo = await DbManager.GetPublicUserInfo(userId);
        return publicUserInfo;
    }
}
