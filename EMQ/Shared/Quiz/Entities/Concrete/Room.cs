using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Quiz.Entities.Concrete;

// Anything that's in this class that's not JsonIgnore'd will be visible to ALL players in a room,
// so be careful not to leak player-specific information.
public sealed class Room : IDisposable
{
    public Room(Guid id, string name, Player owner)
    {
        Id = id;
        Name = name;
        Owner = owner;
        CreatedAt = DateTime.UtcNow;
    }

    private readonly object _lock = new();

    public Guid Id { get; }

    public string Name { get; set; }

    [JsonIgnore]
    public string Password { get; set; } = "";

    public QuizSettings QuizSettings { get; set; } = new();

    public Quiz? Quiz { get; set; }

    // Key: Player.Id
    public ConcurrentDictionary<int, Player> Players { get; set; } = new();

    // Key: Player.Id
    public ConcurrentDictionary<int, Player> Spectators { get; set; } = new();

    public ConcurrentQueue<Player> HotjoinQueue { get; set; } = new();

    public Player Owner { get; set; }

    public DateTime CreatedAt { get; }

    [JsonIgnore]
    public ConcurrentDictionary<int, string> AllConnectionIds { get; set; } = new();

    public TreasureRoom[][] TreasureRooms { get; set; } = Array.Empty<TreasureRoom[]>();

    public ConcurrentQueue<ChatMessage> Chat { get; set; } = new();

    [JsonIgnore]
    public ConcurrentQueue<RoomLog> RoomLog { get; set; } = new();

    public bool CanJoinDirectly => Quiz == null || Quiz.QuizState.QuizStatus != QuizStatus.Playing;

    [JsonIgnore]
    public Dictionary<int, string> PlayerGuesses => Players.ToDictionary(x => x.Key, x => x.Value.Guess);

    public void Dispose()
    {
        Quiz?.Dispose();
    }

    public void Log(string message, int playerId = -1, bool writeToChat = false, bool writeToConsole = true)
    {
        var roomLog = new RoomLog(Id, Quiz?.Id ?? Guid.Empty, QuizSettings, Quiz?.QuizState ?? null, playerId, message);
        RoomLog.Enqueue(roomLog);

        if (writeToConsole)
        {
            Console.WriteLine(roomLog.ToString());
        }

        if (writeToChat)
        {
            Chat.Enqueue(new ChatMessage(message));
        }
    }

    public void RemovePlayer(Player toRemove)
    {
        while (Players.ContainsKey(toRemove.Id))
        {
            Players.TryRemove(toRemove.Id, out _);
        }
    }

    public void RemoveSpectator(Player toRemove)
    {
        while (Spectators.ContainsKey(toRemove.Id))
        {
            Spectators.TryRemove(toRemove.Id, out _);
        }

        // toRemove may or may not be here
        HotjoinQueue = new ConcurrentQueue<Player>(HotjoinQueue.Where(x => x != toRemove));
    }

    public void AddPlayer(Player player)
    {
        while (!Players.ContainsKey(player.Id))
        {
            Players.TryAdd(player.Id, player);
        }
    }

    public void AddSpectator(Player spectator)
    {
        while (!Spectators.ContainsKey(spectator.Id))
        {
            Spectators.TryAdd(spectator.Id, spectator);
        }
    }
}
