namespace Juliet.Model.Filters;

// https://code.blicky.net/yorhel/vndb/src/commit/29bccb3a5f661dc60eee1a8b3c24c776aa8c30c6/lib/VNWeb/AdvSearch.pm#L36
public abstract class Query
{
    public static string ToJsonNormalized(Query query, ref string ret, bool isRoot)
    {
        var list = new List<Query>();
        switch (query)
        {
            case Combinator cc:
                if (isRoot)
                {
                    ret += "[";
                    ret += "\"" + cc.Kind.GetDescription() + "\"";
                    ret += ",";
                }

                list.AddRange(cc.Query);
                break;
            case Predicate pp:
                list.Add(pp);
                break;
        }

        foreach (Query cQuery in list)
        {
            if (ret.Any() && ret.Last() != ',')
            {
                ret += ",";
            }

            switch (cQuery)
            {
                case Combinator c:
                    ret += "[" + "\"" + c.Kind.GetDescription() + "\"" + ",";
                    ToJsonNormalized(c, ref ret, false);
                    ret += "]";
                    break;
                case Predicate p:
                    ret +=
                        "[" + "\"" + p.Name.GetDescription() + "\"" + "," +
                        "\"" + p.Operator.GetDescription() + "\"" + "," +
                        "\"" + p.Value + "\"" + "]";
                    break;
            }
        }

        if (isRoot && query is Combinator)
        {
            ret += "]";
        }

        return ret;
    }
}
