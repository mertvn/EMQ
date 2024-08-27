using System;

namespace EMQ.Shared.Erodle.Entities.Concrete.Dto.Request;

public class ReqGetErodle
{
    public DateOnly Date { get; set; }

    public ErodleKind Kind { get; set; }

    public int UserId { get; set; }
}
