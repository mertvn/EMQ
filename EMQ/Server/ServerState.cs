using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Business;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace EMQ.Server;

public static class ServerState
{
    private static readonly object s_serverStateLock = new();

    // We cannot use a ConcurrentDictionary here, because we can't get references after indexing
    // TODO: would be better if this was a ConcurrentQueue
    public static ImmutableList<Room> Rooms { get; private set; } = ImmutableList<Room>.Empty;

    // TODO: would be better if this was a ConcurrentQueue
    public static ImmutableList<QuizManager> QuizManagers { get; private set; } = ImmutableList<QuizManager>.Empty;

    // TODO: would be better if this was a ConcurrentQueue
    public static ImmutableList<Session> Sessions { get; private set; } = ImmutableList<Session>.Empty;

    public static bool AllowGuests { get; set; } = true;

    public static bool RememberGuestsBetweenServerRestarts { get; set; } = false;

    public static bool IsServerReadOnly { get; set; } = false; // todo check this in more places

    public static bool IsSubmissionDisabled { get; set; } = false;

    public static ConcurrentQueue<EmailQueueItem> EmailQueue { get; set; } = new();

    public static readonly ConcurrentDictionary<int, ConcurrentQueue<InvocationMessage>> PumpMessages = new();

    public static readonly ConcurrentDictionary<int, Thread> PumpThreads = new();

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
        lock (s_serverStateLock)
        {
            room.Dispose();
            if (room.Quiz != null)
            {
                // we can't call RemoveQuizManager here because of the lock
                var qm = QuizManagers.First(x => x.Quiz.Id == room.Quiz.Id);

                int oldQMCount = QuizManagers.Count;
                QuizManagers = QuizManagers.Remove(qm);
                int newQMCount = QuizManagers.Count;

                if (oldQMCount <= newQMCount)
                {
                    Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                    Console.WriteLine("concurrency warning (qm3)");
                    Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                    // throw new Exception();
                }
            }

            // room.Quiz = null;

            int oldRoomsCount = Rooms.Count;
            Rooms = Rooms.Remove(room);
            int newRoomsCount = Rooms.Count;

            if (oldRoomsCount <= newRoomsCount)
            {
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("concurrency warning (room)");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                // throw new Exception();
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
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("concurrency warning (qm)");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                // throw new Exception();
            }
        }
    }

    public static void RemoveSession(Session session, string source)
    {
        Console.WriteLine($"Removing session for p{session.Player.Id} {session.Player.Username}. Source: {source}");
        lock (s_serverStateLock)
        {
            var oldRoomPlayer = ServerState.Rooms.SingleOrDefault(x => x.Players.Any(y => y.Id == session.Player.Id));
            var oldRoomSpec = ServerState.Rooms.SingleOrDefault(x => x.Spectators.Any(y => y.Id == session.Player.Id));
            if (oldRoomPlayer is not null)
            {
                oldRoomPlayer.RemovePlayer(session.Player);
                oldRoomPlayer.Log($"{session.Player.Username} left the room.", -1, true);

                if (!oldRoomPlayer.Players.Any())
                {
                    ServerState.RemoveRoom(oldRoomPlayer, "RemoveSession");
                }
            }
            else if (oldRoomSpec is not null)
            {
                oldRoomSpec.RemoveSpectator(session.Player);
                oldRoomSpec.Log($"{session.Player.Username} left the room.", -1, true);

                if (!oldRoomSpec.Players.Any())
                {
                    ServerState.RemoveRoom(oldRoomSpec, "RemoveSession");
                }
            }

            int oldSessionsCount = Sessions.Count;
            Sessions = Sessions.Remove(session);
            int newSessionsCount = Sessions.Count;

            if (oldSessionsCount <= newSessionsCount)
            {
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("concurrency warning (session)");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                // throw new Exception();
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
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("concurrency warning (room2)");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                // throw new Exception();
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
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("concurrency warning (qm2)");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                //throw new Exception();
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
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("concurrency warning (session2)");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                // throw new Exception();
            }
        }
    }
}
