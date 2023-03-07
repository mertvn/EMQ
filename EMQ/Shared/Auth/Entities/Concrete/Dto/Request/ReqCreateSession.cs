using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqCreateSession
{
    public ReqCreateSession(string username, string password, PlayerVndbInfo vndbInfo)
    {
        Username = username;
        Password = password;
        VndbInfo = vndbInfo;
    }

    [MinLength(1)]
    public string Username { get; }

    public string Password { get; }

    public PlayerVndbInfo VndbInfo { get; }
}
