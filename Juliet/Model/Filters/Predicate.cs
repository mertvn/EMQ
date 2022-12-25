using System.ComponentModel;
using Newtonsoft.Json;

namespace Juliet.Model.Filters;

// https://code.blicky.net/yorhel/vndb/src/commit/29bccb3a5f661dc60eee1a8b3c24c776aa8c30c6/lib/VNWeb/AdvSearch.pm#L36
// i hate everything about this
public class Predicate : Query
{
    public Predicate(string name, FilterOperator @operator, dynamic value)
    {
        Name = name;
        Operator = @operator;
        Value = value;
    }

    [JsonProperty(Order = 1)]
    public string Name { get; init; }

    [JsonProperty(Order = 2)]
    public FilterOperator Operator { get; init; }

    [JsonProperty(Order = 3)]
    public dynamic Value { get; init; }
}

public class Combinator : Query
{
    public Combinator(string kind, IEnumerable<Query> query)
    {
        Kind = kind;
        Query = query;
    }

    public string Kind { get; init; }

    public IEnumerable<Query> Query { get; init; }

    // todo accept Query instead of Combinator?
    public string ToJsonNormalized(Combinator comb, ref string ret, bool isRoot)
    {
        if (isRoot)
        {
            ret += "[";
            ret += "\"" + Kind + "\"";
            ret += ",";
        }

        foreach (Query cQuery in comb.Query)
        {
            switch (cQuery)
            {
                case Combinator c:
                    // Console.WriteLine("proc c");
                    ret += "[" + "\"" + c.Kind + "\"" + ",";
                    ToJsonNormalized(c, ref ret, false);
                    ret += "]";
                    // ProcessCombinator(c, ref ret);
                    break;
                case Predicate p:
                    // Console.WriteLine("proc p");
                    ret += "," +
                           "[" + "\"" + p.Name + "\"" + "," +
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

public abstract class Query
{
}

public enum FilterOperator
{
    [Description("=")]
    Equal,

    [Description("!=")]
    NotEqual,

    [Description(">=")]
    GreaterEqual,

    [Description(">")]
    GreaterThan,

    [Description("<=")]
    LesserEqual,

    [Description("<")]
    LessThan
}
