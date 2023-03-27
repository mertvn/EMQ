using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqSetVndbInfo
{
    public ReqSetVndbInfo(string playerToken, PlayerVndbInfo vndbInfo)
    {
        PlayerToken = playerToken;
        VndbInfo = vndbInfo;
    }

    public string PlayerToken { get; }

    public PlayerVndbInfo VndbInfo { get; }
}
