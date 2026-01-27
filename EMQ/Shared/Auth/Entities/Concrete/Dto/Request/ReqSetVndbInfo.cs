using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqSetVndbInfo
{
    public ReqSetVndbInfo(string playerToken, List<PlayerVndbInfo> vndbInfo)
    {
        PlayerToken = playerToken;
        VndbInfo = vndbInfo;
    }

    public string PlayerToken { get; }

    public List<PlayerVndbInfo> VndbInfo { get; }
}
