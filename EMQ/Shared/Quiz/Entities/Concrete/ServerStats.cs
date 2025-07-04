﻿using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ServerStats
{
    public int RoomsCount { get; set; }

    public int QuizManagersCount { get; set; }

    public int ActiveSessionsCount { get; set; }

    public int SessionsCount { get; set; }

    public ServerConfig Config { get; set; } = new();

    public string GitHash { get; set; } = "";

    public CountdownInfo CountdownInfo { get; set; } = new();
}
