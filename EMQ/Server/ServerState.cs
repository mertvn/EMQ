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
}
