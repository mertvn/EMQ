namespace EMQ.Shared.Erodle.Entities.Concrete;

public class ErodleContainer
{
    public Core.SharedDbEntities.Erodle Erodle { get; set; } = new();

    public ErodleStatus Status { get; set; }
}
