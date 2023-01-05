using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Quiz.Entities.Concrete;

// Anything that's in this class that's not JsonIgnore'd will be visible to ALL players in a room,
// so be careful not to leak player-specific information.
// TODO: Other players' guesses are leaked currently (but hidden with CSS).
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

    [JsonIgnore]
    public string Password { get; set; } = "";

    public QuizSettings QuizSettings { get; set; } = new();

    public Quiz? Quiz { get; set; }

    public List<Player> Players { get; set; } = new();

    public Player Owner { get; set; }

    [JsonIgnore]
    public List<string> AllPlayerConnectionIds { get; set; } = new(); // todo needs to be dict

    public TreasureRoom[][] TreasureRooms { get; set; } = Array.Empty<TreasureRoom[]>();
}

// GREAT API DESIGN BTW! doesn't even work
// [JsonSerializable(typeof(Room))]
// [JsonSerializable(typeof(int))]
// [JsonSerializable(typeof(string))]
// [JsonSerializable(typeof(QuizSettings))]
// [JsonSerializable(typeof(Quiz))]
// [JsonSerializable(typeof(List<Player>))]
// [JsonSerializable(typeof(Player))]
// [JsonSerializable(typeof(List<string>))]
// [JsonSerializable(typeof(TreasureRoom))]
// [JsonSerializable(typeof(TreasureRoom[]))]
// [JsonSerializable(typeof(TreasureRoom[][]))]
// [JsonSerializable(typeof(Treasure))]
// public partial class SourceGenerationContext : JsonSerializerContext
// {
//     // public static SourceGenerationContext Default { get; }
//     //
//     // public JsonTypeInfo<Room>? Room { get; }
//     //
//     // public SourceGenerationContext(JsonSerializerOptions? options) : base(options)
//     // {
//     // }
//     //
//     // public override JsonTypeInfo? GetTypeInfo(Type type) => throw new NotImplementedException();
//     //
//     // protected override JsonSerializerOptions? GeneratedSerializerOptions { get; }
// }
