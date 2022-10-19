using System;
using System.Collections.Generic;
using BlazorApp1.Server.Model;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Auth.Dto.Request;
using BlazorApp1.Shared.Quiz.Dto.Response;
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

    public static readonly List<Session> Sessions = new() { };

    [HttpPost]
    [Route("CreateSession")]
    public ResCreateSession CreateSession([FromBody] ReqCreateSession request)
    {
        // todo: authenticate with db and get playerId
        int playerId = -1;

        if (request.Username == "" && request.Password == "")
        {
            playerId = 69;
        }

        if (request.Username == "test" && request.Password == "")
        {
            playerId = 1001;
        }

        string token = Guid.NewGuid().ToString();
        Sessions.Add(new Session(playerId, token));

        return new ResCreateSession(playerId, token);
    }
}
