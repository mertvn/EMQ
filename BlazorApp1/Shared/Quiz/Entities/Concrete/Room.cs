using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class Room
{
    public int Id { get; set; } = -1;

    [JsonIgnore] public Guid Guid { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    [JsonIgnore] public string Password { get; set; } = "";

    public Quiz? Quiz { get; set; }

    public List<Player> Players { get; set; } = new();
}
