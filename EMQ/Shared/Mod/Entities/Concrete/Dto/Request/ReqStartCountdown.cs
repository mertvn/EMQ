using System;

namespace EMQ.Shared.Mod.Entities.Concrete.Dto.Request;

public class ReqStartCountdown
{
    public ReqStartCountdown(string message, DateTime dateTime)
    {
        Message = message;
        DateTime = dateTime;
    }

    public string Message { get; }

    public DateTime DateTime { get; }
}
