using System.Text;

namespace Juliet.Model.Filters;

// https://code.blicky.net/yorhel/vndb/src/commit/29bccb3a5f661dc60eee1a8b3c24c776aa8c30c6/lib/VNWeb/AdvSearch.pm#L36
public abstract class Query
{
    public static string ToJsonNormalized(Query query, bool isRoot, StringBuilder? ret = null)
    {
        ret ??= new StringBuilder();

        var list = new List<Query>();
        switch (query)
        {
            case Combinator cc:
                if (isRoot)
                {
                    ret.Append($"[\"{cc.Kind.GetDescription()}\",");
                }

                list.AddRange(cc.Query);
                break;
            case Predicate pp:
                list.Add(pp);
                break;
        }

        foreach (Query cQuery in list)
        {
            if (ret.Length > 0 && ret[^1] != ',')
            {
                ret.Append(',');
            }

            switch (cQuery)
            {
                case Combinator c:
                    ret.Append($"[\"{c.Kind.GetDescription()}\",");
                    ToJsonNormalized(c, false, ret);
                    ret.Append(']');
                    break;
                case Predicate p:
                    ret.Append($"[\"{p.Name.GetDescription()}\",\"{p.Operator.GetDescription()}\",\"{p.Value}\"]");
                    break;
            }
        }

        if (isRoot && query is Combinator)
        {
            ret.Append(']');
        }

        return ret.ToString();
    }
}
