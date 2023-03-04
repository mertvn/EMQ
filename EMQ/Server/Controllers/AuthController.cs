using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using EMQ.Shared.VNDB.Business;
using Juliet.Model.VNDBObject;
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

        List<Label>? vns = null;
        if (!string.IsNullOrWhiteSpace(req.VndbInfo.VndbId))
        {
            var labels = new List<Label>();
            VNDBLabel[] vndbLabels = await VndbMethods.GetLabels(req.VndbInfo);

            foreach (VNDBLabel vndbLabel in vndbLabels)
            {
                labels.Add(Label.FromVndbLabel(vndbLabel));
            }

            // we try including playing, finished, stalled, voted, EMQ-wl, and EMQ-bl labels by default
            foreach (Label label in labels)
            {
                switch (label.Name.ToLowerInvariant())
                {
                    case "playing":
                    case "finished":
                    case "stalled":
                    case "voted":
                    case "emq-wl":
                        label.Kind = LabelKind.Include;
                        break;
                    case "emq-bl":
                        label.Kind = LabelKind.Exclude;
                        break;
                }
            }

            vns = await VndbMethods.GrabPlayerVNsFromVndb(
                new PlayerVndbInfo()
                {
                    VndbId = req.VndbInfo.VndbId, VndbApiToken = req.VndbInfo.VndbApiToken, Labels = labels,
                }
            );
        }

        var player = new Player(playerId, req.Username) { Avatar = new Avatar(AvatarCharacter.Auu, "default"), };

        // todo: invalidate previous session with the same playerId if it exists
        string token = Guid.NewGuid().ToString();

        var session = new Session(player, token);
        session.VndbInfo = new PlayerVndbInfo()
        {
            VndbId = req.VndbInfo.VndbId, VndbApiToken = req.VndbInfo.VndbApiToken, Labels = vns
        };
        ServerState.Sessions.Add(session);
        _logger.LogInformation($"Created new session for {player.Id} {player.Username} ({session.VndbInfo.VndbId})");

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
        if (session != null)
        {
            ServerState.Sessions.Remove(session);
        }
        // todo notify room?
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
            var label = session.VndbInfo.Labels.FirstOrDefault(x => x.Id == req.Label.Id);
            if (label == null)
            {
                label = req.Label;
                session.VndbInfo.Labels.Add(label);
            }

            Console.WriteLine($"{session.VndbInfo.VndbId}: " + label.Id + ", " + label.Kind + " => " + req.Label.Kind);
            label.Kind = req.Label.Kind;

            var newVnUrls = new List<string>();
            switch (req.Label.Kind)
            {
                case LabelKind.Maybe:
                    break;
                case LabelKind.Include:
                case LabelKind.Exclude:
                    var grabbed = await VndbMethods.GrabPlayerVNsFromVndb(new PlayerVndbInfo()
                    {
                        VndbId = session.VndbInfo.VndbId,
                        VndbApiToken = session.VndbInfo.VndbApiToken,
                        Labels = new List<Label>() { label },
                    });
                    newVnUrls = grabbed.SingleOrDefault()?.VnUrls ?? new List<string>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            label.VnUrls = newVnUrls;
            return label;
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
}
