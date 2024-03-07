using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;
using ProtoBuf;

namespace EMQ.Shared.Core;

public static class ExtensionMethods
{
    public static string Base64Encode(this string plainText)
    {
        byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    public static string Base64Decode(this string base64EncodedData)
    {
        byte[] base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }

    public static bool IsVideoLink(this string s)
    {
        return s.EndsWith(".webm") || s.EndsWith(".mp4");
    }

    public static string ToVndbUrl(this string? vndbId)
    {
        return "https://vndb.org/" + vndbId;
    }

    public static string ToVndbId(this string vndbUrl)
    {
        return vndbUrl.Replace("https://vndb.org/", "");
    }

    public static string SanitizeVndbAdvsearchStr(this string vndbAdvsearchStr)
    {
        try
        {
            // accept both full urls and just the f param
            var match = Regex.Match(vndbAdvsearchStr, "f=(.+)");
            return match.Success
                ? match.Groups[1].Value.Split('&')[0]
                : vndbAdvsearchStr;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return vndbAdvsearchStr;
        }
    }

    public static bool IsReachableFromCoords(this Point point, int x, int y,
        int maxDistance = LootingConstants.MaxDistance)
    {
        if (Math.Abs(point.X - x) < maxDistance &&
            Math.Abs(point.Y - y) < maxDistance)
        {
            return true;
        }

        return false;
    }

    // Falls back to name
    public static string? GetDescription(this Enum value)
    {
        Type type = value.GetType();
        string? name = Enum.GetName(type, value);
        if (name != null)
        {
            FieldInfo? field = type.GetField(name);
            if (field != null)
            {
                if (Attribute.GetCustomAttribute(field,
                        typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                {
                    return attr.Description;
                }
                else
                {
                    return name;
                }
            }
        }

        return null;
    }

    // Falls back to name
    public static string? GetDisplayName(this Enum value)
    {
        Type type = value.GetType();
        string? name = Enum.GetName(type, value);
        if (name != null)
        {
            FieldInfo? field = type.GetField(name);
            if (field != null)
            {
                if (Attribute.GetCustomAttribute(field,
                        typeof(DisplayAttribute)) is DisplayAttribute attr)
                {
                    return attr.Name;
                }
                else
                {
                    return name;
                }
            }
        }

        return null;
    }

    public static RangeAttribute? GetRange(this Enum value)
    {
        Type type = value.GetType();
        string? name = Enum.GetName(type, value);
        if (name != null)
        {
            FieldInfo? field = type.GetField(name);
            if (field != null)
            {
                if (Attribute.GetCustomAttribute(field,
                        typeof(RangeAttribute)) is RangeAttribute attr)
                {
                    return attr;
                }
                else
                {
                    return null;
                }
            }
        }

        return null;
    }

    public static T GetEnum<T>(this string description) where T : Enum
    {
        foreach (T enumItem in Enum.GetValues(typeof(T)))
        {
            if (enumItem.GetDescription() == description)
            {
                return enumItem;
            }
        }

        throw new ArgumentException("Not found.", nameof(description));
        // return default;
    }

    public static async Task<bool> DownloadFile(this HttpClient client, string dest, Uri uri)
    {
        try
        {
            Console.WriteLine($"DownloadFile {uri}");
            await using (var stream = await client.GetStreamAsync(uri))
            await using (var fs = new FileStream(dest, FileMode.OpenOrCreate))
            {
                await stream.CopyToAsync(fs);
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    public static string LastSegment(this string input)
    {
        return new Uri(input).Segments.Last();
    }

    public static SongLite ToSongLite(this Song song)
    {
        var sourcesDict = new Dictionary<string, List<SongSourceSongType>>();
        foreach (SongSource source in song.Sources)
        {
            sourcesDict[source.Links.Single(x => x.Type == SongSourceLinkType.VNDB).Url.ToVndbId()] = source.SongTypes;
        }

        var songLite = new SongLite(
            song.Titles,
            song.Links,
            sourcesDict,
            song.Artists.Select(artist => artist.VndbId ?? "").ToList(),
            song.Id,
            song.Stats);
        return songLite;
    }

    public static SongLite_MB ToSongLite_MB(this Song song)
    {
        var songLite = new SongLite_MB(
            song.MusicBrainzRecordingGid!.Value,
            song.Links,
            song.Id,
            song.Stats);
        return songLite;
    }

    public static string NormalizeForAutocomplete(this string input)
    {
        // todo Coμ, √ after and another
        // todo ☆, ♪ etc.
        return new string(input
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD)
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .Where(y => (char.IsLetterOrDigit(y) || char.IsWhiteSpace(y)))
                .ToArray())
            .Normalize(NormalizationForm.FormC);
    }

    public static string ReplaceSelfhostLink(this string url)
    {
        if (Constants.SelfhostAddress is null)
        {
            throw new Exception();
        }

        return url.Replace("https://emqselfhost", Constants.SelfhostAddress);
    }

    public static string UnReplaceSelfhostLink(this string url)
    {
        if (Constants.SelfhostAddress is null)
        {
            throw new Exception();
        }

        return url.Replace(Constants.SelfhostAddress, "https://emqselfhost");
    }

    public static string SerializeToBase64String_PB<T>(this T obj)
    {
        // Tested out compressing with gzip (brotli is unavailable on browser) before encoding as base64,
        // but it resulted in only 4-8% smaller size; decided it's not worth the added complexity/processing time.
        using (MemoryStream ms = new())
        {
            Serializer.Serialize(ms, obj);
            return Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);
        }
    }

    public static T DeserializeFromBase64String_PB<T>(this string str)
    {
        byte[] arr = Convert.FromBase64String(str);
        using (MemoryStream ms = new(arr))
        {
            return Serializer.Deserialize<T>(ms);
        }
    }

    public static SongSourceSongType[] ToSongSourceSongTypes(this SongSourceSongTypeMode mode)
    {
        SongSourceSongType[] songSourceSongTypes = mode switch
        {
            SongSourceSongTypeMode.All => new[]
            {
                SongSourceSongType.OP, SongSourceSongType.ED, SongSourceSongType.Insert, SongSourceSongType.BGM
            },
            SongSourceSongTypeMode.Vocals => new[]
            {
                SongSourceSongType.OP, SongSourceSongType.ED, SongSourceSongType.Insert
            },
            SongSourceSongTypeMode.BGM => new[] { SongSourceSongType.BGM },
            _ => throw new InvalidOperationException()
        };

        return songSourceSongTypes;
    }

    public static long GetIdentityValue(this object o)
    {
        PropertyInfo[] properties = o.GetType().GetProperties();
        foreach (PropertyInfo property in properties)
        {
            if (Attribute.GetCustomAttribute(property, typeof(DatabaseGeneratedAttribute)) is DatabaseGeneratedAttribute
                {
                    DatabaseGeneratedOption: DatabaseGeneratedOption.Identity
                })
            {
                var type = property.PropertyType;
                long? value = type switch
                {
                    not null when type == typeof(int) => property.GetValue(o) as int?,
                    not null when type == typeof(long) => property.GetValue(o) as long?,
                    _ => throw new Exception("only int and long identity properties are supported")
                };

                return value ?? 0;
            }
        }

        return 0;
    }

    public static int DetermineSongStartTime(this Song song, QuizFilters? filters)
    {
        int duration = (int)SongLink.GetShortestLink(song.Links).Duration.TotalSeconds;
        int startTimeStart = 0;
        int startTimeEnd = duration;
        const int leeway = 40;

        if (filters != null)
        {
            startTimeStart = duration * filters.StartTimePercentageStart / 100;
            startTimeEnd = duration * filters.StartTimePercentageEnd / 100;
        }

        return Random.Shared.Next(startTimeStart, Math.Clamp(duration - leeway, startTimeStart, startTimeEnd));
    }

    public static UserSpacedRepetition DoSM2(this UserSpacedRepetition previous, bool isCorrect)
    {
        const float minEase = 1.7f;
        int grade = isCorrect ? 4 : 2;
        var ret = new UserSpacedRepetition
        {
            ease = Math.Max(minEase, previous.ease + (0.1f - (5 - grade) * (0.08f + (5 - grade) * 0.02f)))
        };

        if (grade < 3)
        {
            ret.n = 0;
            ret.interval_days = 1;
        }
        else
        {
            ret.n = previous.n + 1;
            ret.interval_days = previous.n switch
            {
                0 => 1,
                1 => 6,
                _ => (float)Math.Ceiling(previous.interval_days * ret.ease)
            };
        }

        // safeguard against intervals that may go over database/language limits
        ret.interval_days = Math.Min(99_999, ret.interval_days);
        return ret;
    }
}
