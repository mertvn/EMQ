using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Core;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqStartRegistration
{
    public ReqStartRegistration(string username, string email, string code)
    {
        Username = username;
        Email = email;
        Code = code;
    }

    [Required]
    [RegularExpression(RegexPatterns.UsernameRegex)]
    public string Username { get; set; }

    [Required]
    // <input type="email"> validates it on the client, explicit validation is also performed on the server
    // [RegularExpression(RegexPatterns.EmailRegex)]
    public string Email { get; set; }

    public string Code { get; set; }
}
