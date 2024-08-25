namespace EMQ.Shared.Erodle.Entities.Concrete.Dto.Request;

public class ReqUpsertStatus
{
    public int ErodleId { get; set; }

    public ErodleStatus Status { get; set; }
}
