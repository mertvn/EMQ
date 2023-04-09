using System;
using System.Collections.Generic;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Business;

namespace EMQ.Server;

public static class ServerState
{
    public static readonly List<Room> Rooms = new() { };
    public static readonly List<QuizManager> QuizManagers = new() { };
    public static readonly List<Session> Sessions = new() { };

    public static void CleanupRoom(Room room)
    {
        Console.WriteLine($"Cleaning up r{room.Id} {room.Name}");

        room.Dispose();
        ServerState.QuizManagers.RemoveAll(x => x.Quiz.Id == room.Quiz?.Id);
        // room.Quiz = null;
        ServerState.Rooms.Remove(room);
    }
}
