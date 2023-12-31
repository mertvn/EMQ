using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqStoreUserQuizSettings
{
    public ReqStoreUserQuizSettings(string playerToken, string name, string b64)
    {
        PlayerToken = playerToken;
        Name = name;
        B64 = b64;
    }

    [Required]
    public string PlayerToken { get; }

    [Required]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "Preset name must be between 1 and 64 characters long.")]
    public string Name { get; }

    [StringLength(short.MaxValue, MinimumLength = 1)]
    [Required]
    public string B64 { get; }
}
