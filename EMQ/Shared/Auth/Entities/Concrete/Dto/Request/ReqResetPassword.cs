using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqResetPassword
{
    public ReqResetPassword(int userId, string token, string newPassword)
    {
        UserId = userId;
        Token = token;
        NewPassword = newPassword;
    }

    [Required]
    public int UserId { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [StringLength(AuthStuff.MaxPasswordLength, MinimumLength = AuthStuff.MinPasswordLength)]
    public string NewPassword { get; }
}
