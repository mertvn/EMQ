using EMQ.Shared.Auth.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

public class ResCreateSession
{
    public ResCreateSession(Session session)
    {
        Session = session;
    }

    public Session Session { get; }
}
