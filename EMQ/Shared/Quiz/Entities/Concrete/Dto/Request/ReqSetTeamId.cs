using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqSetTeamId
{
    public ReqSetTeamId(int value, int userId)
    {
        Value = value;
        UserId = userId;
    }

    public int Value { get; }

    public int UserId { get; }
}
