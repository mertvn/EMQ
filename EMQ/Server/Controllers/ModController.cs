using System;
using System.Threading.Tasks;
using EMQ.Server.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EMQ.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class ModController : ControllerBase
{
    public ModController(ILogger<ModController> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<ModController> _logger;

    [HttpGet]
    [Route("ExportSongLite")]
    public async Task<ActionResult<string>> ExportSongLite([FromQuery] string adminPassword)
    {
        string? envVar = Environment.GetEnvironmentVariable("EMQ_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(envVar) || envVar != adminPassword)
        {
            Console.WriteLine("Rejected ExportSongLite request");
            return Unauthorized();
        }

        Console.WriteLine("Approved ExportSongLite request");
        return await DbManager.ExportSongLite();
    }
}
