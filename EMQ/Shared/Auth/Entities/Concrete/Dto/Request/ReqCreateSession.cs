using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqCreateSession
{
    // used when logging in
    [JsonConstructor]
    public ReqCreateSession(string usernameOrEmail, string password, PlayerVndbInfo vndbInfo)
    {
        UsernameOrEmail = usernameOrEmail;
        Password = password;
        VndbInfo = vndbInfo;
    }

    // used when user is already logged in
    public ReqCreateSession(int userId, string token, PlayerVndbInfo vndbInfo)
    {
        UserId = userId;
        Token = token;
        VndbInfo = vndbInfo;
    }

    public string UsernameOrEmail { get; } = "";

    public string Password { get; } = "";

    public int UserId { get; }

    public string Token { get; } = "";

    public PlayerVndbInfo VndbInfo { get; }
}
