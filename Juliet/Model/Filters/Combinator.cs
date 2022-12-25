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

    // todo accept Query instead of Combinator?
    public string ToJsonNormalized(Combinator comb, ref string ret, bool isRoot)
    {
        if (isRoot)
        {
            ret += "[";
            ret += "\"" + Kind.GetDescription() + "\"";
            ret += ",";
        }

        foreach (Query cQuery in comb.Query)
        {
            switch (cQuery)
            {
                case Combinator c:
                    // Console.WriteLine("proc c");
                    ret += "[" + "\"" + c.Kind.GetDescription() + "\"" + ",";
                    ToJsonNormalized(c, ref ret, false);
                    ret += "]";
                    // ProcessCombinator(c, ref ret);
                    break;
                case Predicate p:
                    // Console.WriteLine("proc p");
                    ret += "," +
                           "[" + "\"" + p.Name.GetDescription() + "\"" + "," +
                           "\"" + p.Operator.GetDescription() + "\"" + "," +
                           "\"" + p.Value + "\"" + "]";
                    // Console.WriteLine($"ret: {ret}");
                    break;
            }
        }

        if (isRoot)
        {
            ret += "]";
        }

        ret = ret.Replace(",,", ",").Replace(",]", "]").Replace("][", "],[");
        // Console.WriteLine($"ret: {ret}");
        return ret;
    }
}
