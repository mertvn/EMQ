namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

public class ResJoinRoom
{
    public ResJoinRoom(int waitMs)
    {
        WaitMs = waitMs;
    }

    public int WaitMs { get; }
}
