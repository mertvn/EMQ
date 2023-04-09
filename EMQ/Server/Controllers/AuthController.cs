using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<AuthController> _logger;

    [HttpPost]
    [Route("CreateSession")]
    public async Task<ResCreateSession> CreateSession([FromBody] ReqCreateSession req)
    {
        // todo: authenticate with db and get player
        int playerId;
        do
        {
            playerId = new Random().Next();
        } while (ServerState.Sessions.Any(x => x.Player.Id == playerId));

        var player = new Player(playerId, req.Username) { Avatar = new Avatar(AvatarCharacter.Auu, "default"), };

        // todo: invalidate previous session with the same playerId if it exists
        string token = Guid.NewGuid().ToString();

        var session = new Session(player, token);
        session.VndbInfo = new PlayerVndbInfo() { VndbId = req.VndbInfo.VndbId, Labels = req.VndbInfo.Labels };

        ServerState.Sessions.Add(session);

        string? ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        string? header = (Request.HttpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ??
                          Request.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault());
        if (IPAddress.TryParse(header, out IPAddress? i))
        {
            ip = i.ToString();
        }

        _logger.LogInformation(
            $"Created new session for p{player.Id} {player.Username} ({session.VndbInfo.VndbId}) @ {ip}");

        // var claims = new List<Claim>
        // {
        //     new Claim(ClaimTypes.Sid, player.Id.ToString()),
        //     new Claim(ClaimTypes.Name, player.Username),
        //     new Claim(ClaimTypes.Role, "User"),
        // };
        //
        // var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        // var authProperties = new AuthenticationProperties
        // {
        //     AllowRefresh = true,
        //     IsPersistent = true,
        //
        //     //RedirectUri = <string>
        //     // The full path or absolute URI to be used as an http
        //     // redirect response value.
        // };
        //
        // await HttpContext.SignInAsync(
        //     CookieAuthenticationDefaults.AuthenticationScheme,
        //     new ClaimsPrincipal(claimsIdentity),
        //     authProperties);

        return new ResCreateSession(session);
    }

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
            room.Players.RemoveAll(x => x.Id == session.Player.Id);
            room.AllPlayerConnectionIds.Remove(session.Player.Id);
            if (!room.Players.Any())
            {
                ServerState.CleanupRoom(room);
            }
        }

        ServerState.Sessions.Remove(session);
    }

    [HttpPost]
    [Route("ValidateSession")]
    public async Task<ActionResult<Session>> ValidateSession([FromBody] string token)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == token);
        return session == null ? Unauthorized() : session;
    }

    [HttpPost]
    [Route("UpdateLabel")]
    public async Task<Label> UpdateLabel([FromBody] ReqUpdateLabel req)
    {
        var session = ServerState.Sessions.Single(x => x.Token == req.PlayerToken); // todo check session
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

    [HttpPost]
    [Route("SetVndbInfo")]
    public async Task<ActionResult> SetVndbInfo([FromBody] ReqSetVndbInfo req)
    {
        var session = ServerState.Sessions.SingleOrDefault(x => x.Token == req.PlayerToken);
        if (session != null)
        {
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
        else
        {
            return Unauthorized();
        }
    }
}
