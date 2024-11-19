using System.Collections.Generic;

namespace EMQ.Shared.Library.Entities.Concrete.Dto;

public class ResFindQueueItemsWithPendingChanges
{
    public Dictionary<int, int> RQs { get; set; } = new();

    public Dictionary<int, int> EQs { get; set; } = new();
}
