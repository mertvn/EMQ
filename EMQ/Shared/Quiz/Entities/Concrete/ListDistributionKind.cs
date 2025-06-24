using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum ListDistributionKind
{
    Random = 0,
    // BalancedStrict = 1,
    Balanced = 2,
    // Unread = 3,
    CappedRandom = 4,
}
