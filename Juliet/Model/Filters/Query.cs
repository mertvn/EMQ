namespace Juliet.Model.Filters;

// https://code.blicky.net/yorhel/vndb/src/commit/29bccb3a5f661dc60eee1a8b3c24c776aa8c30c6/lib/VNWeb/AdvSearch.pm#L36
public abstract class Query
{
    // todo maybe this should accept a list of queries?
    public static string ToJsonNormalized(Query query, ref string ret, bool isRoot)
    {
        if (query is Combinator cc)
        {
            if (isRoot)
            {
                ret += "[";
                ret += "\"" + cc.Kind.GetDescription() + "\"";
                ret += ",";
            }

            foreach (Query cQuery in cc.Query)
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

            if (isRoot)
            {
                ret += "]";
            }
        }
        else if (query is Predicate pp)
        {
            if (ret.Any() && ret.Last() != ',')
            {
                ret += ",";
            }

            ret +=
                "[" + "\"" + pp.Name.GetDescription() + "\"" + "," +
                "\"" + pp.Operator.GetDescription() + "\"" + "," +
                "\"" + pp.Value + "\"" + "]";
        }

        return ret;
    }
}
