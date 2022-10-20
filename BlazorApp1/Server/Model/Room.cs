using System.Collections.Generic;
using System.Linq;
using BlazorApp1.Server.Controllers;
using BlazorApp1.Shared.Quiz;
using BlazorApp1.Shared.Quiz.Entities.Concrete;

namespace BlazorApp1.Server.Model;

public class Room
{
    public int Id { get; set; } = -1;

    public string Name { get; set; } = "";

    public string Password { get; set; } = "";

    public Quiz? Quiz { get; set; }

    public List<Player> Players { get; set; } = new();

    public string[] ConnectionIds =>
        AuthController.Sessions.Where(x => Players.Select(y => y.Id).Contains(x.PlayerId)).Select(x => x.ConnectionId!)
            .ToArray();
}
