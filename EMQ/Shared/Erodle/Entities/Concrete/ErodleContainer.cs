using System.Collections.Generic;

namespace EMQ.Shared.Erodle.Entities.Concrete;

public class ErodleContainer
{
    public Core.SharedDbEntities.Erodle Erodle { get; set; } = new();

    public ErodleStatus Status { get; set; }

    public List<ErodleAnswer> PreviousAnswers { get; set; } = new();
}
