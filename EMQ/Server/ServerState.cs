using System;
using System.Collections.Concurrent;
using System.Linq;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Business;

namespace EMQ.Server;

public static class ServerState
{
    private static readonly object s_serverStateLock = new();

    // TODO: would be better if this was a ConcurrentDictionary
    public static ConcurrentQueue<Room> Rooms { get; private set; } = new();

    // TODO: would be better if this was a ConcurrentDictionary
    public static ConcurrentQueue<QuizManager> QuizManagers { get; private set; } = new();

    // TODO: would be better if this was a ConcurrentDictionary
    public static ConcurrentQueue<Session> Sessions { get; private set; } = new();

    public static void RemoveRoom(Room room, string source)
    {
        Console.WriteLine($"Removing r{room.Id} {room.Name}. Source: {source}");
        lock (s_serverStateLock)
        {
            room.Dispose();
            if (room.Quiz != null)
            {
                var qm = QuizManagers.Single(x => x.Quiz.Id == room.Quiz.Id);

                int oldQMCount = QuizManagers.Count;
                QuizManagers = new ConcurrentQueue<QuizManager>(QuizManagers.Where(x => x != qm));
                int newQMCount = QuizManagers.Count;

                if (oldQMCount <= newQMCount)
                {
                    throw new Exception();
                }
            }

            // room.Quiz = null;

            int oldRoomsCount = Rooms.Count;
            Rooms = new ConcurrentQueue<Room>(Rooms.Where(x => x != room));
            int newRoomsCount = Rooms.Count;

            if (oldRoomsCount <= newRoomsCount)
            {
                throw new Exception();
            }
        }
    }

    public static void RemoveSession(Session session, string source)
    {
        Console.WriteLine($"Removing session for p{session.Player.Id} {session.Player.Username}. Source: {source}");
        lock (s_serverStateLock)
        {
            int oldSessionsCount = Rooms.Count;
            Sessions = new ConcurrentQueue<Session>(Sessions.Where(x => x != session));
            int newSessionsCount = Rooms.Count;

            if (oldSessionsCount <= newSessionsCount)
            {
                throw new Exception();
            }
        }
    }
}
