using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqSetPassword
{
    public ReqSetPassword(string username, string token, string newPassword)
    {
        Username = username;
        Token = token;
        NewPassword = newPassword;
    }

    [Required]
    public string Username { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [StringLength(AuthStuff.MaxPasswordLength, MinimumLength = AuthStuff.MinPasswordLength)]
    public string NewPassword { get; }
}
