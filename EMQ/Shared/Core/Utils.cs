using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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

    public static JsonSerializerOptions JsoIndentedNotDefault { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
        TypeInfoResolver = new IgnoreEmptyValuesResolver(),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static JsonSerializerOptions JsoNotNull { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonSerializerOptions JsoCompact { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonSerializerOptions JsoCompactAggressive { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new IgnoreEmptyValuesResolver(),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    // public static JsonSerializerOptions JsoNoStringEnum { get; } = new()
    // {
    //     Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    // };

    public class IgnoreEmptyValuesResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo typeInfo = base.GetTypeInfo(type, options);
            foreach (JsonPropertyInfo property in typeInfo.Properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    property.ShouldSerialize = (_, val) => !string.IsNullOrEmpty((string?)val);
                }
                else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    property.ShouldSerialize = (_, val) => val is IEnumerable e && e.GetEnumerator().MoveNext();
                }
            }

            return typeInfo;
        }
    }

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

    public static async Task WaitWhile(Func<Task<bool>> condition, int frequency = 25, int timeout = -1)
    {
        var waitTask = Task.Run(async () =>
        {
            while (await condition()) await Task.Delay(frequency);
        });

        await Task.WhenAny(waitTask, Task.Delay(timeout));
    }

    public static (string modStr, int number) ParseVndbScreenshotStr(string screenshot)
    {
        int number = Convert.ToInt32(screenshot.Substring(2, screenshot.Length - 2));
        int mod = number % 100;
        string modStr = mod > 9 ? mod.ToString() : $"0{mod}";
        return (modStr, number);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string UserIdToUsername(Dictionary<int, string> dict, int userId)
    {
        return dict.TryGetValue(userId, out string? username) ? username : $"Guest-{userId}";
    }

    public static (string latinTitle, string? nonLatinTitle) VndbTitleToEmqTitle(string vndbName, string? vndbLatin)
    {
        string latinTitle;
        string? nonLatinTitle;
        if (string.IsNullOrEmpty(vndbLatin))
        {
            latinTitle = vndbName;
            nonLatinTitle = null;
        }
        else
        {
            latinTitle = vndbLatin;
            nonLatinTitle = vndbName;
        }

        return (latinTitle, nonLatinTitle);
    }

    public static string GetReversedArtistName(string? x)
    {
        return string.IsNullOrEmpty(x)
            ? ""
            : string.Join("", x
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Reverse()).NormalizeForAutocomplete();
    }

    public class MyStopwatchSection
    {
        public string Name { get; set; } = "";

        public TimeSpan Elapsed { get; set; }
    }

    public class MyStopwatch
    {
        public Stopwatch Stopwatch { get; } = new();

        public List<MyStopwatchSection> Sections { get; } = new();

        public void StartSection(string name)
        {
            Sections.Add(new MyStopwatchSection { Name = name, Elapsed = Stopwatch.Elapsed });
        }

        public void Start()
        {
            Stopwatch.Start();
        }

        public void Stop()
        {
            StartSection("end");
            Stopwatch.Stop();

            bool shouldPrint = Sections.First().Elapsed > TimeSpan.Zero;
            if (shouldPrint)
            {
                for (int index = 0; index < Sections.Count; index++)
                {
                    MyStopwatchSection current = Sections[index];
                    MyStopwatchSection? next = Sections.ElementAtOrDefault(index + 1);
                    if (next is null)
                    {
                        continue;
                    }

                    Console.WriteLine($"{current.Name}: {(next.Elapsed - current.Elapsed).TotalMilliseconds}ms");
                }
            }
        }
    }
}
