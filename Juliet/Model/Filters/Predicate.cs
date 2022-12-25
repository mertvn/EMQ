using System.ComponentModel;
using Newtonsoft.Json;

namespace Juliet.Model.Filters;

// https://code.blicky.net/yorhel/vndb/src/commit/29bccb3a5f661dc60eee1a8b3c24c776aa8c30c6/lib/VNWeb/AdvSearch.pm#L36
// i hate everything about this
// [JsonConverter(typeof(ObjectToArrayConverter<Predicate>))]
public class Predicate : CombinatorOrPredicate
{
    public Predicate(string name, FilterOperator @operator, dynamic value)
    {
        Name = name;
        Operator = @operator.GetDescription()!;
        Value = value;
    }

    [JsonProperty(Order = 1)]
    public string Name { get; init; }

    [JsonProperty(Order = 2)]
    public string Operator { get; init; }

    [JsonProperty(Order = 3)]
    public dynamic Value { get; init; }
}

// [JsonConverter(typeof(myconv<Combinator>))]
public class Combinator : CombinatorOrPredicate
{
    public Combinator(string kind, IEnumerable<CombinatorOrPredicate> query, bool sameLevel)
    {
        Kind = kind;
        Query = query;
        SameLevel = sameLevel;

        // if (sameLevel)
        // {
        //     v = JsonConvert.SerializeObject(new dynamic[] { Kind, Query });
        // }
        // else
        // {
        //     v = JsonConvert.SerializeObject(new dynamic[] { Kind, new dynamic[] { Query } });
        // }

        // v = v.Remove(v.Length - 1, 1).Remove(0, 1);
    }

    [JsonIgnore]
    // [JsonProperty(Order = 1)]
    public bool SameLevel { get; init; }

    [JsonIgnore]
    [JsonProperty(Order = 2)]
    public string Kind { get; init; }

    [JsonIgnore]
    [JsonProperty(Order = 3)]
    public IEnumerable<CombinatorOrPredicate> Query { get; init; }

    // [JsonProperty(Order = 1)]
    // public string v { get; set; }

    // [JsonProperty(Order = 2)]
    // public Predicate? Predicate1 { get; init; }
    //
    // [JsonProperty(Order = 3)]
    // public Predicate? Predicate2 { get; init; }
    //
    // [JsonProperty(Order = 4)]
    // public Predicate? Predicate3 { get; init; }

    public string ToJsonNormalized(Combinator comb, ref string ret, bool outer)
    {
        if (outer)
        {
            ret += "[";
            ret += "\"" + Kind + "\"";
            ret += ",";
        }

        foreach (CombinatorOrPredicate cQuery in comb.Query)
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
                           "\"" + p.Operator + "\"" + "," +
                           "\"" + p.Value + "\"" + "]";
                    // Console.WriteLine($"ret: {ret}");
                    break;
            }
        }

        if (outer)
        {
            ret += "]";
        }

        ret = ret.Replace(",,", ",").Replace(",]", "]").Replace("][", "],[");
        // Console.WriteLine($"ret: {ret}");
        return ret;
    }
}

public abstract class CombinatorOrPredicate
{
}

// [JsonConverter(typeof(ObjectToArrayConverter<Query>))]
// public class Query // : IEnumerable
// {
//     [JsonProperty(Order = 1)]
//     public Combinator? Combinator { get; init; }
//
//     [JsonProperty(Order = 2)]
//     public Predicate? Predicate { get; init; }
//
//     // public IEnumerator GetEnumerator()
//     // {
//     //     return GetEnumerator1();
//     //
//     // }
//     //
//     // public IEnumerator GetEnumerator1()
//     // {
//     //     yield return Element;
//     // }
// }

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
