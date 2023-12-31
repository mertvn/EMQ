using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqDeleteUserQuizSettings
{
    public ReqDeleteUserQuizSettings(string playerToken, string name)
    {
        PlayerToken = playerToken;
        Name = name;
    }

    [Required]
    public string PlayerToken { get; }

    [Required]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "Preset name must be between 1 and 64 characters long.")]
    public string Name { get; }
}
