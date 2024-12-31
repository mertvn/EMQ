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
using EMQ.Server.Db.Entities.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

// todo? require userid + token everywhere instead of just token?
[CustomAuthorize(PermissionKind.Visitor)]
[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    public AuthController(ILogger<AuthController> logger, IOutputCacheStore outputCache)
    {
        _logger = logger;
        _outputCache = outputCache;
    }

    private readonly ILogger<AuthController> _logger;
    private readonly IOutputCacheStore _outputCache;

    public async Task EvictFromOutputCache(string tag)
    {
        await _outputCache.EvictByTagAsync(tag, default);
    }

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
        AvatarCharacter character = AvatarCharacter.Auu;
        string skin = "Default";

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
                int random = Random.Shared.Next(Constants.PlayerIdGuestMin, Constants.PlayerIdBotMin);
                playerId = Convert.ToInt32(random.ToString()[..7]);
            } while (ServerState.Sessions.Any(x => x.Player.Id == playerId));

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
                userRoleKind = user.roles;
                playerId = user.id;
                character = user.avatar;
                skin = user.skin;

                var existingSession = ServerState.Sessions.SingleOrDefault(x => x.Player.Id == playerId);
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
            var secret = await DbManager_Auth.GetSecret(req.UserId, new Guid(req.Token));
            if (secret is not null)
            {
                var user = await DbManager_Auth.GetEntity_Auth<User>(secret.user_id);
                if (user != null)
                {
                    username = user.username;
                    token = secret.token.ToString();
                    userRoleKind = user.roles;
                    playerId = user.id;
                    character = user.avatar;
                    skin = user.skin;

                    var existingSession = ServerState.Sessions.SingleOrDefault(x => x.Player.Id == playerId);
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

        string? activeUserLabelPresetName = await DbManager_Auth.GetActiveUserLabelPresetName(playerId);
        var vndbInfo = await ServerUtils.GetVndbInfo_Inner(playerId, activeUserLabelPresetName);
        var player = new Player(playerId, username, new Avatar(character, skin));
        var session = new Session(player, token, userRoleKind, activeUserLabelPresetName);

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
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.Token);
        _logger.LogInformation("Removing session " + session?.Token);
        if (session == null)
        {
            return;
        }

        var secret = await DbManager_Auth.GetSecret(session.Player.Id, new Guid(session.Token));

        // enable if guest sessions are ever written to DB
        // if (secret == null)
        // {
        //     throw new Exception("idk"); // todo?
        // }

        if (secret != null)
        {
            await DbManager_Auth.DeleteEntity_Auth(secret);
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
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.Token);

        var secret = await DbManager_Auth.GetSecret(req.Player.Id, new Guid(req.Token));
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
            // todo move this to the end of CreateSession as ValidateSession calls it anyways
            HttpContext.Response.Cookies.Append("user-id", session.Player.Id.ToString(), new CookieOptions
            {
                Domain = $".{Constants.WebsiteDomainNoProtocol}",
                MaxAge = TimeSpan.FromDays(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
                IsEssential = true,
            });

            HttpContext.Response.Cookies.Append("session-token", session.Token, new CookieOptions
            {
                Domain = $".{Constants.WebsiteDomainNoProtocol}",
                MaxAge = TimeSpan.FromDays(1),
                Secure = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
                IsEssential = true,
            });

            vndbInfo ??=
                await ServerUtils.GetVndbInfo_Inner(session.Player.Id, session.ActiveUserLabelPresetName);
            return new ResValidateSession(session, vndbInfo);
        }
    }

    [EnableRateLimiting(RateLimitKind.ValidateSession)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpGet]
    [Route("ValidateSessionWithCookie")]
    public async Task<ActionResult> ValidateSessionWithCookie()
    {
        if (Request.Cookies.TryGetValue("user-id", out string? userId) &&
            Request.Cookies.TryGetValue("session-token", out string? token))
        {
            var session =
                ServerState.Sessions.SingleOrDefault(x => x.Player.Id.ToString() == userId && x.Token == token);
            if (session != null)
            {
                Response.Headers["X-USER-ID"] = session.Player.Id.ToString();
                Response.Headers["X-USER-NAME"] = session.Player.Username;
                Response.Headers["X-USER-ROLE"] =
                    AuthStuff.HasPermission(session.UserRoleKind, PermissionKind.Admin) ? "admin" : "editor";
            }
        }

        return Ok();
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

        PlayerVndbInfo vndbInfo =
            await DbManager_Auth.GetUserVndbInfo(session.Player.Id, session.ActiveUserLabelPresetName);
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
            kind = req.Label.Kind,
            preset_name = session.ActiveUserLabelPresetName!,
        };
        long userLabelId = await DbManager_Auth.RecreateUserLabel(userLabel, req.Label.VNs);

        var userLabelVns = await DbManager_Auth.GetUserLabelVns(userLabelId);
        var label = ServerUtils.FromUserLabel(userLabel);
        label.VNs = userLabelVns.ToDictionary(x => x.vnid, x => x.vote);

        return label;
    }

    // [CustomAuthorize(PermissionKind.UpdatePreferences)]
    // [HttpPost]
    // [Route("UpdatePlayerPreferences")]
    // public async Task<ActionResult<PlayerPreferences>> UpdatePlayerPreferences(
    //     [FromBody] ReqUpdatePlayerPreferences req)
    // {
    //     var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
    //     if (session == null)
    //     {
    //         return Unauthorized();
    //     }
    //
    //     session.Player.Preferences = req.PlayerPreferences;
    //     return session.Player.Preferences;
    // }

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
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session == null)
        {
            return Unauthorized();
        }

        _logger.LogInformation($"SetVndbInfo for p{session.Player.Id} to {req.VndbInfo.VndbId}");

        if (string.IsNullOrEmpty(session.ActiveUserLabelPresetName))
        {
            return StatusCode(520);
        }

        await DbManager_Auth.DeleteUserLabels(session.Player.Id, session.ActiveUserLabelPresetName);

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
                    kind = label.Kind,
                    preset_name = session.ActiveUserLabelPresetName,
                };
                long _ = await DbManager_Auth.RecreateUserLabel(userLabel, label.VNs);
            }
        }

        // todo this is inefficient
        var vndbInfo = await ServerUtils.GetVndbInfo_Inner(session.Player.Id, session.ActiveUserLabelPresetName);
        return vndbInfo;
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("CspReport")]
    public async Task<IActionResult> CspReport([FromBody] dynamic report)
    {
        bool log = false;
        if (log)
        {
            string serialized = (string)JsonSerializer.Serialize(report, Utils.JsoIndented);
            if (!serialized.Contains("blazor.webassembly.js") &&
                !serialized.Contains("moz-extension") &&
                !serialized.Contains("chrome-extension") &&
                !serialized.Contains("google-analytics"))
            {
                _logger.LogError("CSP violation: " + serialized);
            }
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpGet]
    [Route("GetServerStats")]
    [OutputCache(Duration = 10, PolicyName = "MyOutputCachePolicy")]
    public ServerStats GetServerStats()
    {
        return new ServerStats()
        {
            RoomsCount = ServerState.Rooms.Count,
            QuizManagersCount = ServerState.QuizManagers.Count,
            ActiveSessionsCount = ServerState.Sessions.Count(x => x.Player.HasActiveConnection),
            SessionsCount = ServerState.Sessions.Count,
            IsServerReadOnly = ServerState.IsServerReadOnly,
            IsSubmissionDisabled = ServerState.IsSubmissionDisabled,
            GitHash = ServerState.GitHash,
            CountdownInfo = ServerState.CountdownInfo,
        };
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetServerActivityStats")]
    [OutputCache(Duration = 5 * 60, PolicyName = "MyOutputCachePolicy")]
    public async Task<ServerActivityStats> GetServerActivityStats(ReqGetServerActivityStats req)
    {
        return await DbManager.GetServerActivityStats(req.StartDate, req.EndDate);
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpGet]
    [Route("GetRooms")]
    public IEnumerable<Room> GetRooms()
    {
        var ret = ServerState.Rooms.ToList();
        var privateRooms = ret.Where(x => !string.IsNullOrEmpty(x.Password)).Select(x => x.Id).ToHashSet();

        // todo use Clone()
        ret = JsonSerializer.Deserialize<List<Room>>(JsonSerializer.Serialize(ret))!; // need deep-copy
        foreach (Room room in ret)
        {
            room.Chat = null!;
            room.IsPrivate = privateRooms.Contains(room.Id);
        }

        return ret;
    }

    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpGet]
    [Route("Ping")]
    public void Ping()
    {
    }

    [EnableRateLimiting(RateLimitKind.ValidateSession)]
    [CustomAuthorize(PermissionKind.Visitor)]
    [HttpPost]
    [Route("IsUsernameAvailable")]
    public async Task<ActionResult<bool>> IsUsernameAvailable([FromBody] string username)
    {
        return await DbManager_Auth.IsUsernameAvailable(username);
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

    // todo important publicly shared quiz settings presets
    [CustomAuthorize(PermissionKind.StoreQuizSettings)]
    [HttpGet]
    [Route("GetUserQuizSettings")]
    public async Task<ActionResult<List<ResGetUserQuizSettings>>> GetUserQuizSettings(string token)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == token);
        if (session is null)
        {
            return Unauthorized();
        }

        return await DbManager_Auth.SelectUserQuizSettings(session.Player.Id);
    }

    [CustomAuthorize(PermissionKind.StoreQuizSettings)]
    [HttpPost]
    [Route("StoreUserQuizSettings")]
    public async Task<ActionResult> StoreUserQuizSettings([FromBody] ReqStoreUserQuizSettings req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        await DbManager_Auth.InsertUserQuizSettings(session.Player.Id, req.Name, req.B64);
        Console.WriteLine($"p{session.Player.Id} {session.Player.Username} saved preset {req.Name} {req.B64.Length}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.StoreQuizSettings)]
    [HttpPost]
    [Route("DeleteUserQuizSettings")]
    public async Task<ActionResult> DeleteUserQuizSettings([FromBody] ReqDeleteUserQuizSettings req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session is null)
        {
            return Unauthorized();
        }

        await DbManager_Auth.DeleteUserQuizSettings(session.Player.Id, req.Name);
        Console.WriteLine($"p{session.Player.Id} {session.Player.Username} deleted preset {req.Name}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetPublicUserInfo")]
    public async Task<ActionResult<ResGetPublicUserInfo>> GetPublicUserInfo([FromBody] int userId)
    {
        var publicUserInfo = await DbManager.GetPublicUserInfo(userId);
        return publicUserInfo != null ? publicUserInfo : StatusCode(404);
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpGet]
    [OutputCache(Duration = 5 * 60, PolicyName = "MyOutputCachePolicy")]
    [Route("GetPublicUserInfoSongs")]
    public async Task<ActionResult<string?>> GetPublicUserInfoSongs([FromQuery] int userId)
    {
        var publicUserInfo = await DbManager.GetPublicUserInfoSongs(userId);
        return publicUserInfo != null ? publicUserInfo : StatusCode(404);
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetUserStats")]
    public async Task<List<UserStat>> GetUserStats()
    {
        var userStats = await DbManager.GetUserStats();
        return userStats;
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpGet]
    [Route("GetUserLabelPresets")]
    public async Task<ActionResult<List<UserLabelPreset>>> GetUserLabelPresets()
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        return await DbManager_Auth.GetUserLabelPresets(session.Player.Id);
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("UpsertUserLabelPreset")]
    public async Task<ActionResult<PlayerVndbInfo>> UpsertUserLabelPreset([FromBody] string name)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        bool success =
            await DbManager_Auth.UpsertUserLabelPreset(new UserLabelPreset
            {
                user_id = session.Player.Id, name = name
            });
        if (success)
        {
            Console.WriteLine($"p{session.Player.Id} {session.Player.Username} upserted user label preset {name}");
            session.ActiveUserLabelPresetName = name;
            var vndbInfo =
                await ServerUtils.GetVndbInfo_Inner(session.Player.Id, session.ActiveUserLabelPresetName);
            return vndbInfo;
        }
        else
        {
            return StatusCode(520);
        }
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("DeleteUserLabelPreset")]
    public async Task<ActionResult> DeleteUserLabelPreset([FromBody] string name)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await DbManager_Auth.DeleteUserLabelPreset(new UserLabelPreset { user_id = session.Player.Id, name = name });
        session.ActiveUserLabelPresetName = null;
        Console.WriteLine($"p{session.Player.Id} {session.Player.Username} deleted user label preset {name}");
        return Ok();
    }

    [CustomAuthorize(PermissionKind.UpdatePreferences)]
    [HttpPost]
    [Route("SetAvatar")]
    public async Task<ActionResult<Avatar>> SetAvatar([FromBody] Avatar req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        if (req.Character is AvatarCharacter.VNDBCharacterImage)
        {
            req.Skin = await DbManager.GetCharacterImageId(req.Skin.ToVndbId());
        }
        else
        {
            if (!req.IsValidSkinForCharacter())
            {
                return StatusCode(520);
            }
        }

        await DbManager_Auth.SetAvatar(session.Player.Id, req);
        session.Player.Avatar = req;
        return req;
    }

    [CustomAuthorize(PermissionKind.Vote)]
    [HttpPost]
    [Route("UpsertMusicVote")]
    public async Task<ActionResult<MusicVote>> UpsertMusicVote([FromBody] ReqUpsertMusicVote req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        bool success;
        MusicVote musicVote;
        if (req.Vote is null)
        {
            musicVote = new MusicVote { music_id = req.MusicId, user_id = session.Player.Id, };
            success = await DbManager.DeleteEntity(musicVote);
        }
        else
        {
            musicVote = new MusicVote
            {
                music_id = req.MusicId,
                user_id = session.Player.Id,
                vote = req.Vote.Value,
                updated_at = DateTime.UtcNow,
            };
            success = await DbManager.UpsertEntity(musicVote);
        }

        if (success)
        {
            await EvictFromOutputCache("all");
            Console.WriteLine(
                $"p{session.Player.Id} {session.Player.Username} upserted music vote {req.MusicId} = {req.Vote}");
            return musicVote;
        }
        else
        {
            return StatusCode(520);
        }
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetUserMusicVotes")]
    public async Task<ActionResult<MusicVote[]>> GetUserMusicVotes([FromBody] int userId)
    {
        return await DbManager.GetUserMusicVotes(userId, SongSourceSongTypeMode.All);
    }

    [CustomAuthorize(PermissionKind.ViewStats)]
    [HttpPost]
    [Route("GetMusicVotes")]
    public async Task<ActionResult<ResGetMusicVotes>> GetMusicVotes([FromBody] int musicId)
    {
        return await DbManager.GetMusicVotes(musicId);
    }
}
