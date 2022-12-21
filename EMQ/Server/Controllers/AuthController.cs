using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Shared.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
using EMQ.Shared.VNDB.Business;
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
        int playerId = new Random().Next();

        var vns = new List<string>();
        if (!string.IsNullOrWhiteSpace(req.VndbInfo.VndbId))
        {
            vns = await VndbMethods.GrabPlayerVNsFromVNDB(new PlayerVndbInfo()
            {
                VndbId = req.VndbInfo.VndbId, VndbApiToken = req.VndbInfo.VndbApiToken
            });
        }

        var player = new Player(playerId, req.Username)
        {
            Avatar = new Avatar(AvatarCharacter.Auu, "default"),
            VndbInfo = new PlayerVndbInfo()
            {
                VndbId = req.VndbInfo.VndbId, VndbApiToken = req.VndbInfo.VndbApiToken, VNs = vns
            }
        };

        // todo: invalidate previous session with the same playerId if it exists
        string token = Guid.NewGuid().ToString();
        var session = new Session(player, token);
        ServerState.Sessions.Add(session);
        _logger.LogInformation("Created new session for player " + player.Id + $" ({player.VndbInfo.VndbId})");

        return new ResCreateSession(session);
    }

    [HttpPost]
    [Route("RemoveSession")]
    public async Task RemoveSession([FromBody] ReqRemoveSession req)
    {
        var session = ServerState.Sessions.Single(x => x.Token == req.Token);
        _logger.LogInformation("Removing session " + session.Token);
        ServerState.Sessions.Remove(session);
        // todo notify room?
    }
}
