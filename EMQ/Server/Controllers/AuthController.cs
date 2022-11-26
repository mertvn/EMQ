using System;
using System.Collections.Generic;
using EMQ.Shared.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;
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
    public ResCreateSession CreateSession([FromBody] ReqCreateSession req)
    {
        // todo: authenticate with db and get player
        int playerId = new Random().Next();

        // if (req.Username == "" && req.Password == "")
        // {
        //     playerId = 69;
        // }
        //
        // if (req.Username == "test" && req.Password == "")
        // {
        //     playerId = 1001;
        // }

        var player = new Player(playerId, req.Username) { Avatar = new Avatar(AvatarCharacter.Auu, "default") };
        // todo: invalidate previous session with the same playerId if it exists

        _logger.LogInformation("Created new session for player " + player.Id);
        string token = Guid.NewGuid().ToString();
        ServerState.Sessions.Add(new Session(player, token));

        return new ResCreateSession(playerId, token);
    }
}
