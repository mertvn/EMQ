using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EMQ.Shared.Core;

public static class Utils
{
    public static JsonSerializerOptions Jso => new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new JsonStringEnumConverter() }
    };

    public static JsonSerializerOptions JsoIndented => new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static JsonSerializerOptions JsoNotNull => new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string PercentageStr(int dividend, int divisor)
    {
        return $"{(((double)dividend / divisor) * 100):N2}%";
    }

    public static string PercentageStr(double dividend, double divisor)
    {
        return $"{((dividend / divisor) * 100):N2}%";
    }

    public static string FixFileName(string name)
    {
        return string.Join(" ", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}
