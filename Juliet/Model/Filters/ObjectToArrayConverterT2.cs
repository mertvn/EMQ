using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Juliet.Model.Filters;

// https://stackoverflow.com/a/39462464
public class myconv<T> : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(T) == objectType;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var objectType = value.GetType();
        var contract = serializer.ContractResolver.ResolveContract(objectType) as JsonObjectContract;
        if (contract == null)
            throw new JsonSerializationException(string.Format("invalid type {0}.", objectType.FullName));

        Console.WriteLine("objectType: " + objectType);
        // var wroteArray = false;
        // if (objectType == typeof(Combinator))
        // {
        // }
        // else
        // {
        writer.WriteStartArray();
        //     wroteArray = true;
        // }

        JToken t = JToken.FromObject(value);
        Console.WriteLine(t);
        Object o = (JObject)t;
        // o.WriteTo(writer);

        foreach (var property in SerializableProperties(contract))
        {
            //  Console.WriteLine("pName: " + property.PropertyName);
            var propertyValue = property.ValueProvider.GetValue(value);

            if (propertyValue != null) // todo only if NullValueHandling=NullValueHandling.Ignore
            {
                // if (property.PropertyName == "SameLevel")
                // {
                //     if ((bool)propertyValue)
                //     {
                //         continue;
                //     }
                //     else
                //     {
                //         writer.WriteStartArray();
                //         wroteArray = true;
                //     }
                // }


                // if (property.Converter != null && property.Converter.CanWrite)
                //     property.Converter.WriteJson(writer, propertyValue, serializer);
                // else
                //     serializer.Serialize(writer, propertyValue);
            }
        }

        // if (wroteArray)
        // {
        writer.WriteEndArray();
        // }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");

    }

    static IEnumerable<JsonProperty> SerializableProperties(JsonObjectContract contract)
    {
        return contract.Properties.Where(p => !p.Ignored && p.Readable && p.Writable);
    }
}
