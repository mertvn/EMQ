using System;
using System.Data;
using Dapper;
using Newtonsoft.Json;

namespace EMQ.Server.Db;

public class JsonTypeHandler : SqlMapper.ITypeHandler
{
    public void SetValue(IDbDataParameter parameter, object value)
    {
        parameter.Value = JsonConvert.SerializeObject(value);
    }

    public object Parse(Type destinationType, object value)
    {
        return JsonConvert.DeserializeObject((string)value, destinationType)!;
    }
}
