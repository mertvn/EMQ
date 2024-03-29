﻿using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EMQ.Shared.Core;

public static class Utils
{
    public static JsonSerializerOptions Jso { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new JsonStringEnumConverter() }
    };

    public static JsonSerializerOptions JsoIndented { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static JsonSerializerOptions JsoNotNull { get; } = new()
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

    public static async Task WaitWhile(Func<bool> condition, int frequency = 25, int timeout = -1)
    {
        var waitTask = Task.Run(async () =>
        {
            while (condition()) await Task.Delay(frequency);
        });

        await Task.WhenAny(waitTask, Task.Delay(timeout));
    }
}
