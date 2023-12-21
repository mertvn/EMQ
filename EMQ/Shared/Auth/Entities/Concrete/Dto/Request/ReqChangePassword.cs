using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqChangePassword
{
    public ReqChangePassword(string username, string currentPassword, string newPassword)
    {
        Username = username;
        CurrentPassword = currentPassword;
        NewPassword = newPassword;
    }

    [Required]
    public string Username { get; set; }

    [Required]
    public string CurrentPassword { get; set; }

    [Required]
    [StringLength(AuthStuff.MaxPasswordLength, MinimumLength = AuthStuff.MinPasswordLength)]
    public string NewPassword { get; }
}
