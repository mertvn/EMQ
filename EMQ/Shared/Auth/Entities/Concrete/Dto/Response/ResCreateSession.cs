using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResCreateSession
{
    public ResCreateSession(Session session, List<PlayerVndbInfo> vndbInfo)
    {
        Session = session;
        VndbInfo = vndbInfo;
    }

    public Session Session { get; }

    public List<PlayerVndbInfo> VndbInfo { get; set; }
}
