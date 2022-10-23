using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class Room
{
    public Room(int id, string name, Player owner)
    {
        Id = id;
        Name = name;
        Owner = owner;
    }

    public int Id { get; }

    // [JsonIgnore] public Guid Guid { get; set; } = Guid.NewGuid();

    public string Name { get; set; }

    [JsonIgnore] public string Password { get; set; } = "";

    public Quiz? Quiz { get; set; }

    public List<Player> Players { get; set; } = new();

    public Player Owner { get; set; }

    [JsonIgnore] public List<string> AllPlayerConnectionIds { get; set; } = new();
}
