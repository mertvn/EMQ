using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqForgottenPassword
{
    public ReqForgottenPassword(string email)
    {
        Email = email;
    }

    [Required]
    // <input type="email"> validates it on the client, explicit validation is also performed on the server
    // [RegularExpression(RegexPatterns.EmailRegex)]
    public string Email { get; set; }
}
