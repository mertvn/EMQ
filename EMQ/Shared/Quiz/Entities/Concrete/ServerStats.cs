namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ServerStats
{
    public int RoomsCount { get; set; }

    public int QuizManagersCount { get; set; }

    public int ActiveSessionsCount { get; set; }

    public int SessionsCount { get; set; }

    public bool IsServerReadOnly { get; set; } // todo? move to a class like ServerInfo

    public bool IsSubmissionDisabled{ get; set; } // todo? move to a class like ServerInfo
}
