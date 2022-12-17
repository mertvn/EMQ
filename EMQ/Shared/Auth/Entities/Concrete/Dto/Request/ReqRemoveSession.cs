namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqRemoveSession
{
    public ReqRemoveSession(string token)
    {
        Token = token;
    }

    public string Token { get; }
}
