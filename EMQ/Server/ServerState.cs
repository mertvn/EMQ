using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Business;

namespace EMQ.Server;

public static class ServerState
{
    // Key: Room.Id
    public static ConcurrentDictionary<Guid, Room> Rooms { get; } = new();

    // Key: Quiz.Id
    public static ConcurrentDictionary<Guid, QuizManager> QuizManagers { get; } = new();

    // Key: Session.Token
    public static ConcurrentDictionary<string, Session> Sessions { get; } = new();

    public static bool AllowGuests { get; set; } = true;

    public static bool RememberGuestsBetweenServerRestarts { get; set; } = false;

    public static bool IsServerReadOnly { get; set; } = false; // todo check this in more places

    public static bool IsSubmissionDisabled { get; set; } = false;

    public static ConcurrentQueue<EmailQueueItem> EmailQueue { get; set; } = new();

    private static string? s_gitHash;

    public static string GitHash
    {
        get
        {
            if (string.IsNullOrEmpty(s_gitHash))
            {
                string version = "1.0.0+LOCALBUILD"; // Dummy version for local dev
                var appAssembly = typeof(ServerState).Assembly;
                var infoVerAttr = (AssemblyInformationalVersionAttribute?)appAssembly
                    .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute)).FirstOrDefault();

                if (infoVerAttr != null && infoVerAttr.InformationalVersion.Length > 6)
                {
                    version = infoVerAttr.InformationalVersion;
                }

                s_gitHash = version[(version.IndexOf('+') + 1)..];
            }

            return s_gitHash[..7];
        }
    }

    public static void RemoveRoom(Room room, string source)
    {
        Console.WriteLine($"Removing r{room.Id} {room.Name}. Source: {source}");

        room.Dispose();
        if (room.Quiz != null)
        {
            RemoveQuizManager(room.Quiz);
        }

        // room.Quiz = null;
        while (Rooms.ContainsKey(room.Id))
        {
            Rooms.TryRemove(room.Id, out _);
        }
    }

    public static void RemoveQuizManager(Quiz quiz)
    {
        Console.WriteLine($"Removing qm{quiz.Id}");
        while (QuizManagers.ContainsKey(quiz.Id))
        {
            QuizManagers.TryRemove(quiz.Id, out _);
        }
    }

    public static void RemoveSession(Session session, string source)
    {
        Console.WriteLine($"Removing session for p{session.Player.Id} {session.Player.Username}. Source: {source}");
        while (Sessions.ContainsKey(session.Token))
        {
            Sessions.TryRemove(session.Token, out _);
        }
    }

    public static void AddRoom(Room room)
    {
        while (!Rooms.ContainsKey(room.Id))
        {
            Rooms.TryAdd(room.Id, room);
        }
    }

    public static void AddQuizManager(QuizManager quizManager)
    {
        while (!QuizManagers.ContainsKey(quizManager.Quiz.Id))
        {
            QuizManagers.TryAdd(quizManager.Quiz.Id, quizManager);
        }
    }

    public static void AddSession(Session session)
    {
        while (!Sessions.ContainsKey(session.Token))
        {
            Sessions.TryAdd(session.Token, session);
        }
    }
}
