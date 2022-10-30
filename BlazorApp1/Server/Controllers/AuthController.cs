using System;
using System.Collections.Generic;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Auth.Entities.Concrete;
using BlazorApp1.Shared.Auth.Entities.Concrete.Dto.Request;
using BlazorApp1.Shared.Quiz.Entities.Concrete;
using BlazorApp1.Shared.Quiz.Entities.Concrete.Dto.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BlazorApp1.Server.Controllers;

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

        var player = new Player(playerId, req.Username) { Avatar = new Avatar("Assets/Au.png") }; // todo
        // todo: invalidate previous session with the same playerId if it exists

        _logger.LogInformation("Created new session for player " + player.Id);
        string token = Guid.NewGuid().ToString();
        ServerState.Sessions.Add(new Session(player, token));

        return new ResCreateSession(playerId, token);
    }
}
