using System.Collections.Generic;
using BlazorApp1.Shared.Quiz;

namespace BlazorApp1.Server.Model;

public class Room
{
    public int Id { get; set; } = -1;

    public string Name { get; set; } = "";

    public string Password { get; set; } = "";

    public Quiz? Quiz { get; set; }
}
