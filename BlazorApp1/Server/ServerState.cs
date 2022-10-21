using System.Collections.Generic;
using BlazorApp1.Server.Business;
using BlazorApp1.Shared.Auth.Entities.Concrete;
using BlazorApp1.Shared.Quiz.Entities.Concrete;

namespace BlazorApp1.Server;

public static class ServerState
{
    public static readonly List<Room> Rooms = new() { };
    public static readonly List<QuizManager> QuizManagers = new() { };
    public static readonly List<Session> Sessions = new() { };
}
