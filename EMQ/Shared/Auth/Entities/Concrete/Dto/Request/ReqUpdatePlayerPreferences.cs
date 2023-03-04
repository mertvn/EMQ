using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqUpdatePlayerPreferences
{
    public ReqUpdatePlayerPreferences(string playerToken, PlayerPreferences playerPreferences)
    {
        PlayerToken = playerToken;
        PlayerPreferences = playerPreferences;
    }

    public string PlayerToken { get; }

    public PlayerPreferences PlayerPreferences { get; set; }
}
