using System;
using System.Collections.Generic;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Auth.Entities.Concrete;
using BlazorApp1.Shared.Auth.Entities.Concrete.Dto.Request;
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
        // todo: authenticate with db and get playerId
        int playerId = -1;

        if (req.Username == "" && req.Password == "")
        {
            playerId = 69;
        }

        if (req.Username == "test" && req.Password == "")
        {
            playerId = 1001;
        }

        _logger.LogInformation("Created new session for playerId " + playerId);
        string token = Guid.NewGuid().ToString();
        ServerState.Sessions.Add(new Session(playerId, token));

        return new ResCreateSession(playerId, token);
    }
}
