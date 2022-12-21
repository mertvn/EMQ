using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqCreateSession
{
    public ReqCreateSession(string username, string password, PlayerVndbInfo vndbInfo, string? vndbApiToken)
    {
        Username = username;
        Password = password;
        VndbInfo = vndbInfo;
        VndbApiToken = vndbApiToken;
    }

    public string Username { get; }

    public string Password { get; }

    public PlayerVndbInfo VndbInfo { get; }

    public string? VndbApiToken { get; set; }
}
