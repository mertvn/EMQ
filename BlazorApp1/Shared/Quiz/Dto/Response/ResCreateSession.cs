namespace BlazorApp1.Shared.Quiz.Dto.Response;

public class ResCreateSession
{
    public ResCreateSession(int playerId, string token)
    {
        PlayerId = playerId;
        Token = token;
    }

    public int PlayerId { get; }

    public string Token { get; }
}
