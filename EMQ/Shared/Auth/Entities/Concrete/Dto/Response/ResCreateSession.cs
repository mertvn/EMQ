﻿using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResCreateSession
{
    public ResCreateSession(Session session, PlayerVndbInfo vndbInfo)
    {
        Session = session;
        VndbInfo = vndbInfo;
    }

    public Session Session { get; }

    public PlayerVndbInfo VndbInfo { get; set; }
}
