using System.Data;
using Dapper;

namespace EMQ.Server.Db;

public class GenericArrayHandler<T> : SqlMapper.TypeHandler<T[]>
{
    public override void SetValue(IDbDataParameter parameter, T[]? value)
    {
        parameter.Value = value;
    }

    public override T[] Parse(object value)
    {
        return (T[])value;
    }
}
