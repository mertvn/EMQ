using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Business;

namespace EMQ.Server;

public static class ServerState
{
    private static readonly object s_serverStateLock = new();

    // TODO: would be better if this was a ConcurrentDictionary
    public static ImmutableList<Room> Rooms { get; private set; } = ImmutableList<Room>.Empty;

    // TODO: would be better if this was a ConcurrentDictionary
    public static ImmutableList<QuizManager> QuizManagers { get; private set; } = ImmutableList<QuizManager>.Empty;

    // TODO: would be better if this was a ConcurrentDictionary
    public static ImmutableList<Session> Sessions { get; private set; } = ImmutableList<Session>.Empty;

    public static bool AllowGuests { get; set; } = true;

    public static bool RememberGuestsBetweenServerRestarts { get; set; } = true;

    public static bool IsServerReadOnly { get; set; } = false; // todo check this in more places

    public static bool IsSubmissionDisabled { get; set; } = false;

    public static ConcurrentQueue<EmailQueueItem> EmailQueue { get; set; } = new();

    public static void RemoveRoom(Room room, string source)
    {
        Console.WriteLine($"Removing r{room.Id} {room.Name}. Source: {source}");
        lock (s_serverStateLock)
        {
            room.Dispose();
            if (room.Quiz != null)
            {
                var qm = QuizManagers.First(x => x.Quiz.Id == room.Quiz.Id);

                int oldQMCount = QuizManagers.Count;
                QuizManagers = QuizManagers.Remove(qm);
                int newQMCount = QuizManagers.Count;

                if (oldQMCount <= newQMCount)
                {
                    throw new Exception();
                }
            }

            // room.Quiz = null;

            int oldRoomsCount = Rooms.Count;
            Rooms = Rooms.Remove(room);
            int newRoomsCount = Rooms.Count;

            if (oldRoomsCount <= newRoomsCount)
            {
                throw new Exception();
            }
        }
    }

    public static void RemoveQuizManager(Quiz quiz)
    {
        Console.WriteLine($"Removing qm{quiz.Id}");
        lock (s_serverStateLock)
        {
            var qm = QuizManagers.First(x => x.Quiz.Id == quiz.Id);

            int oldQMCount = QuizManagers.Count;
            QuizManagers = QuizManagers.Remove(qm);
            int newQMCount = QuizManagers.Count;

            if (oldQMCount <= newQMCount)
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
            int oldSessionsCount = Sessions.Count;
            Sessions = Sessions.Remove(session);
            int newSessionsCount = Sessions.Count;

            if (oldSessionsCount <= newSessionsCount)
            {
                throw new Exception();
            }
        }
    }

    public static void AddRoom(Room session)
    {
        lock (s_serverStateLock)
        {
            int oldRoomsCount = Rooms.Count;
            Rooms = Rooms.Add(session);
            int newRoomsCount = Rooms.Count;

            if (oldRoomsCount >= newRoomsCount)
            {
                throw new Exception();
            }
        }
    }

    public static void AddQuizManager(QuizManager quizManager)
    {
        lock (s_serverStateLock)
        {
            int oldQuizManagersCount = QuizManagers.Count;
            QuizManagers = QuizManagers.Add(quizManager);
            int newQuizManagersCount = QuizManagers.Count;

            if (oldQuizManagersCount >= newQuizManagersCount)
            {
                throw new Exception();
            }
        }
    }

    public static void AddSession(Session session)
    {
        lock (s_serverStateLock)
        {
            int oldSessionsCount = Sessions.Count;
            Sessions = Sessions.Add(session);
            int newSessionsCount = Sessions.Count;

            if (oldSessionsCount >= newSessionsCount)
            {
                throw new Exception();
            }
        }
    }
}
