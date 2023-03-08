namespace Juliet.Model.Filters;

public class Combinator : Query
{
    public Combinator(CombinatorKind kind, IEnumerable<Query> query)
    {
        Kind = kind;
        Query = query;
    }

    public CombinatorKind Kind { get; init; }

    public IEnumerable<Query> Query { get; init; }
}
