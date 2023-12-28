using System.Text.Json.Serialization;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqCreateSession
{
    // used when logging in
    [JsonConstructor]
    public ReqCreateSession(string usernameOrEmail, string password, bool isGuest)
    {
        UsernameOrEmail = usernameOrEmail;
        Password = password;
        IsGuest = isGuest;
    }

    // used when user is already logged in and has a Secret, like in internal auth flows such as SetPassword etc.
    public ReqCreateSession(int userId, string token)
    {
        UserId = userId;
        Token = token;
    }

    public string UsernameOrEmail { get; } = "";

    public string Password { get; } = "";

    public int UserId { get; }

    public string Token { get; } = "";

    public bool IsGuest { get; }
}
