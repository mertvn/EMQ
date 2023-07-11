using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VNDBStaffNotesParser;

public static class Program
{
    internal static void Main(string[] args)
    {
        if (args.Any())
        {
            Parse(args[0]);
        }
        else
        {
            Console.WriteLine("no input given");
        }
    }

    // longer names need to be checked first
    public static List<Dictionary<SongType, List<string>>> SongTypeDicts { get; } =
        new()
        {
            new Dictionary<SongType, List<string>>
            {
                {
                    SongType.OP, new List<string>
                    {
                        "OP",
                        "OPs",
                        "Opening",
                        "Opening Song",
                        "Openings",
                        "OP Theme",
                        "OP Themes",
                        "opening theme",
                        "OP song",
                        "Main theme",
                        "Main themes",
                        "Main theme song",
                        "Theme Song", // idk
                        "Theme songs", // idk
                        "Theme song -", // idk
                        "Theme Song:", // idk
                        "Theme music", // idk
                        "OP,",
                        "OP1",
                        "OP2",
                        "OP3",
                        "OP4",
                        "OP5",
                        "OP6",
                        "OP7",
                        "OP8",
                        "OP9",
                        "OP 1",
                        "OP 2",
                        "ED chorus, PSV OP", // YOLO
                        "start theme",
                        "Main menu theme:"
                    }.OrderByDescending(x => x).ToList()
                }
            },
            new Dictionary<SongType, List<string>>
            {
                {
                    SongType.ED, new List<string>
                    {
                        "ED",
                        "EDs",
                        "Ending",
                        "Ending Song",
                        "ED song",
                        "Endings",
                        "ED Theme",
                        "ED (Chorus)",
                        "ED chorus",
                        "Ending ED",
                        "End ED",
                        "Ending theme",
                        "ending theme song",
                        "ED Theme",
                        "ED Themes",
                        "ED,",
                        "ED1",
                        "ED2",
                        "ED 2",
                        "ED3",
                        "ED4",
                        "ED5",
                        "ED6",
                        "ED7",
                        "ED8",
                        "ED9",
                        "ED 1",
                        "ED 2",
                        "ED 3",
                        "ED A",
                        "ED B",
                        "EDs 1",
                        "BGM, ED", // YOLO
                        "Sub-Ending",
                        "Cast roll theme",
                        "Happy end song",
                        "credits theme"
                    }.OrderByDescending(x => x).ToList()
                }
            },
            new Dictionary<SongType, List<string>>
            {
                {
                    SongType.Insert, new List<string>
                    {
                        "INS",
                        "Insert",
                        "Inserts",
                        "Insert Song",
                        "Insert Songs",
                        "Insert Song-",
                        "Insert Song -",
                        "Insert Song's",
                        "Image song",
                        "Image songs",
                        "Character song",
                        "Epilogue song",
                        "Interlude",
                        "hymn",
                        "hymns",
                        "cieln", // idonteven
                        "cielns", // idonteven
                        "Insert Music", // should be BGM maybe idk
                        "Insert song 1",
                        "Insert song 2",
                        "Insert song 3",
                        "insert song #1",
                        "insert song #2",
                        "insert song #3",
                        "H-scene song",
                        "Plus Communication song",
                        "Plus Communication songs",
                        "extra song",
                        "PV Song",
                        "PV",
                        "Ep. 6 song",
                        "Ep. 8 song",
                        "Fu-Rin-Ka-Zan theme",
                        "Iwasawa's songs",
                        "µ-12's Theme",
                        "Tristar Theme",
                        "Celica Theme",
                        "DLC song",
                        "DLC son",
                        "Promotion movie song",
                        "Featured song",
                        "Apeiria song",
                        "Miu song",
                        "Mashiro song",
                        "Kuon song",
                        "Apeiria song",
                        "Rhythm segments song",
                        "Uguisu route song",
                        "anniversary songs",
                        "Event song"
                    }.OrderByDescending(x => x).ToList()
                }
            },
            new Dictionary<SongType, List<string>>
            {
                { SongType.BGM, new List<string> { "BGM", "BGMs", }.OrderByDescending(x => x).ToList() }
            },
        };

    public static List<ParsedSong> Parse(string input, bool doIntegrityCheck = true)
    {
        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("input is null or empty");
            return new List<ParsedSong>();
        }

        input = Preprocess(input);

        if (!IsProcessable(input))
        {
            Console.WriteLine($"Unprocessable input: {input}");
            return new List<ParsedSong>();
        }

        var jso = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new JsonStringEnumConverter() }
        };

        var songs = new List<ParsedSong>();

        Mode mode = Mode.SongType;
        int cursor = 0;
        var song = new ParsedSong();
        bool foundSongTypeAtStart = false;
        while (cursor < input.Length)
        {
            switch (mode)
            {
                case Mode.BeforeSongType:
                    // TODO: Rewrite this to scan char by char searching for songTypeNames instead of enumerating the dicts
                    // or use IsNextTokenASongType here
                    var possibleIndexOf = new List<int>();
                    foreach (Dictionary<SongType, List<string>> songTypeDict in SongTypeDicts)
                    {
                        foreach ((SongType _, List<string>? value) in songTypeDict)
                        {
                            foreach (string songTypeName in value)
                            {
                                // require a space before and after because we don't want to match inside words
                                var indexesOf = input.AllIndexesOf(" " + songTypeName + " ").ToList();
                                if (indexesOf.Any())
                                {
                                    // +1 because we required there to be a space before the song type
                                    possibleIndexOf.AddRange(indexesOf.Select(x => x + 1));
                                }
                            }
                        }
                    }

                    possibleIndexOf.RemoveAll(x => x < cursor);
                    if (possibleIndexOf.Any())
                    {
                        int nearestIndexOf = possibleIndexOf.Min(i => (Math.Abs(cursor - i), i)).i;
                        song.BeforeType = input.Substring(cursor, nearestIndexOf - cursor);

                        cursor += song.BeforeType.Length;

                        mode = Mode.SongType;
                        goto nextMode;
                    }
                    else
                    {
                        Console.WriteLine($"Skipping shit - input: {input}");
                        break;
                        // throw new Exception();
                    }
                case Mode.SongType:
                    foreach (Dictionary<SongType, List<string>> songTypeDict in SongTypeDicts)
                    {
                        foundSongTypeAtStart = false;
                        foreach ((SongType key, List<string>? value) in songTypeDict)
                        {
                            foreach (string songTypeName in value)
                            {
                                if (cursor + songTypeName.Length > input.Length)
                                {
                                    continue;
                                }

                                string substr = input.Substring(cursor, songTypeName.Length);
                                // Console.WriteLine(substr + "==" + songTypeName);
                                bool foundSongTypeName = string.Equals(substr, songTypeName,
                                    StringComparison.OrdinalIgnoreCase);

                                if (foundSongTypeName)
                                {
                                    foundSongTypeAtStart = true;
                                    song.Type.Add(key);

                                    cursor += songTypeName.Length + 1; // +1 because space
                                    mode = Mode.SongTitle;

                                    goto nextMode;
                                }
                            }
                        }
                    }


                    if (!foundSongTypeAtStart)
                    {
                        mode = Mode.BeforeSongType;
                        goto nextMode;
                    }

                    break;
                case Mode.SongTitle:
                    Console.WriteLine(input[cursor]);
                    switch (input[cursor])
                    {
                        // normal
                        case '"':
                            break;
                        case ' ':
                            if (input[cursor - 1] == ' ' && input[cursor + 1] == '"')
                            {
                                cursor += 1; // band-aid for double space between type and title
                            }

                            break;
                        // parentheses before song title
                        case '(':
                            cursor += 1;
                            mode = Mode.SongTitle;
                            goto nextMode;
                        // multiple types for the same song
                        case '&':
                        case '/':
                            cursor += 1;
                            if (input[cursor] == ' ')
                            {
                                cursor += 1; // skip the space after if there is one
                            }

                            mode = Mode.SongType;
                            goto nextMode;
                        default:
                            if (input[cursor - 1] == '/')
                            {
                                mode = Mode.SongType;
                                goto nextMode;
                            }

                            if (input[cursor - 1] == '"')
                            {
                                Console.WriteLine(
                                    $"Skipping thing with no space between song type and quote - input: {input}");
                                cursor = input.Length;
                                goto nextMode;
                            }

                            // multiple types for the same song
                            if (input[cursor..(cursor + "and".Length)] == "and")
                            {
                                cursor += "and".Length;
                                if (input[cursor] == ' ')
                                {
                                    cursor += 1; // skip the space after if there is one
                                }

                                mode = Mode.SongType;
                                goto nextMode;
                            }

                            throw new Exception($"Invalid start for SongTitle: {input[cursor]}");
                    }

                    string songTitle = "";
                    while (input[++cursor] != '"')
                    {
                        songTitle += input[cursor];
                    }

                    song.Title = songTitle;

                    string serialized = JsonSerializer.Serialize(song);
                    Console.WriteLine("add " + serialized);
                    songs.Add(JsonSerializer.Deserialize<ParsedSong>(serialized)!);

                    int boundsCheck = ++cursor;
                    if (boundsCheck >= 0 && input.Length > boundsCheck)
                    {
                        // todo: abstractize this to reduce duplication
                        switch (input[boundsCheck])
                        {
                            // new song delimited by ',' or ';'
                            case ',':
                            case ';':
                                {
                                    switch (input[boundsCheck + 1].ToString(), input[boundsCheck + 2].ToString())
                                    {
                                        // new song with same song type
                                        case (" ", "\""): // space after comma
                                            cursor = boundsCheck + 2;
                                            mode = Mode.SongTitle;

                                            song = new ParsedSong { Type = song.Type };

                                            goto nextMode;
                                        case ("\"", _): // no space after comma
                                            cursor = boundsCheck + 1;
                                            mode = Mode.SongTitle;

                                            song = new ParsedSong { Type = song.Type };

                                            goto nextMode;
                                        // new song with different song type
                                        default:
                                            cursor = boundsCheck + 2;
                                            mode = Mode.SongType;

                                            song = new ParsedSong();

                                            goto nextMode;
                                    }
                                }
                            case ' ':
                                {
                                    if (IsNextTokenASongType(input, cursor + 1, out int distance,
                                            out SongType songType))
                                    {
                                        // new song with different song type
                                        cursor = boundsCheck + 1;
                                        mode = Mode.SongType;

                                        song = new ParsedSong();

                                        goto nextMode;
                                    }
                                    else if (input[boundsCheck + 1] == '"')
                                    {
                                        // new song with same song type
                                        cursor = boundsCheck + 1;
                                        mode = Mode.SongTitle;

                                        song = new ParsedSong { Type = song.Type };

                                        goto nextMode;
                                    }
                                    else
                                    {
                                        // super dumb shit to handle MultipleWithDifferentSongTypesCommaAndSpaceDelimiterIntoBeforeType
                                        string inp = input[cursor..^1].ToLowerInvariant();
                                        if ((inp.Contains("op") || inp.Contains("ed") || inp.Contains("insert")) &&
                                            !inp.Contains("and") && !inp.Contains('&') && !inp.Contains('('))
                                        {
                                            // new song starting with BeforeSongType
                                            cursor = boundsCheck + 1;
                                            mode = Mode.BeforeSongType;

                                            song = new ParsedSong();

                                            goto nextMode;
                                        }
                                        else
                                        {
                                            goto default; // AfterTitle
                                        }
                                    }
                                }

                            // AfterTitle
                            default:
                                {
                                    int boundsCheck3 = ++cursor;
                                    if (boundsCheck3 >= 0 && input.Length > boundsCheck3)
                                    {
                                        Console.WriteLine(input[boundsCheck3]);
                                        if (input[boundsCheck3] == ' ') // todo terrible hack
                                        {
                                            boundsCheck3 += 1;
                                        }

                                        if (string.Equals(input.Substring(boundsCheck3, "and".Length), "and",
                                                StringComparison.OrdinalIgnoreCase))
                                        {
                                            // new song delimited by "and"
                                            switch (input[boundsCheck3 + 1 + "and".Length])
                                            {
                                                // new song with same song type
                                                case '"':
                                                    cursor = boundsCheck3 + 1 + "and".Length;
                                                    mode = Mode.SongTitle;

                                                    song = new ParsedSong { Type = song.Type };

                                                    goto nextMode;
                                                // new song with different song type
                                                default:
                                                    cursor = boundsCheck3 + 1 + "and".Length;
                                                    mode = Mode.SongType;

                                                    song = new ParsedSong();

                                                    goto nextMode;
                                            }
                                        }
                                        else if (string.Equals(input.Substring(boundsCheck3, "&".Length), "&",
                                                     StringComparison.OrdinalIgnoreCase))
                                        {
                                            // new song delimited by '&'
                                            switch (input[boundsCheck3 + 1 + "&".Length])
                                            {
                                                // new song with same song type
                                                case '"':
                                                    cursor = boundsCheck3 + 1 + "&".Length;
                                                    mode = Mode.SongTitle;

                                                    song = new ParsedSong { Type = song.Type };

                                                    goto nextMode;
                                                // new song with different song type
                                                default:
                                                    cursor = boundsCheck3 + 1 + "&".Length;
                                                    mode = Mode.SongType;

                                                    song = new ParsedSong();

                                                    goto nextMode;
                                            }
                                        }
                                        else
                                        {
                                            cursor -= 2; // todo
                                        }

                                        mode = Mode.AfterSongTitle;
                                        goto nextMode;
                                    }

                                    break;
                                }
                        }
                    }

                    break;
                case Mode.AfterSongTitle:
                    for (int i = 0; i < input.Length; i++)
                    {
                        int nextIndex = cursor + i + 1;
                        if (nextIndex < input.Length)
                        {
                            char c = input[nextIndex];
                            if (c == ',')
                            {
                                cursor = nextIndex + 1;
                                mode = Mode.SongType;

                                song = new ParsedSong();

                                goto nextMode;
                            }

                            songs.Last().AfterTitle += c;
                        }
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();

                    nextMode:
                    Console.WriteLine("goto " + mode);
                    continue;
            }

            break;
        }

        Console.WriteLine("final output: " + JsonSerializer.Serialize(songs, jso));

        if (doIntegrityCheck)
        {
            CheckIntegrity(songs);
        }

        return songs;
    }

    private static bool IsNextTokenASongType(string input, int cursor, out int distance, out SongType songType)
    {
        distance = -1;
        songType = SongType.Unknown;
        foreach (Dictionary<SongType, List<string>> songTypeDict in SongTypeDicts)
        {
            foreach ((SongType key, List<string>? value) in songTypeDict)
            {
                foreach (string songTypeName in value)
                {
                    if (cursor + songTypeName.Length > input.Length)
                    {
                        continue;
                    }

                    string substr = input.Substring(cursor, songTypeName.Length);
                    // Console.WriteLine(substr + "==" + songTypeName);
                    bool foundSongTypeName = string.Equals(substr, songTypeName,
                        StringComparison.OrdinalIgnoreCase);
                    if (foundSongTypeName)
                    {
                        distance = songTypeName.Length;
                        songType = key;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string Preprocess(string input)
    {
        string ret = input;
        ret = ret.Replace("''", "\"");
        ret = ret.Replace("“", "\"");
        ret = ret.Replace("”", "\"");

        if (ret.EndsWith(','))
        {
            ret = ret.Remove(ret.Length - 1, 1);
        }

        return ret;
    }

    private static bool IsProcessable(string input)
    {
        // check if there any quotes
        if (input.Any(c => c == '"'))
        {
            // check for unclosed quotes
            if ((input.Count(c => c == '"') % 2) != 0)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        // check if we start with a quote and there are less than 4 quotes
        if (input.StartsWith('"') && input.Count(c => c == '"') < 4)
        {
            return false;
        }

        return true;
    }

    private static void CheckIntegrity(List<ParsedSong> songs)
    {
        foreach (ParsedSong song in songs)
        {
            if (song.BeforeType.Length > 73)
            {
                throw new Exception($"BeforeType is too long: {song.BeforeType}");
            }

            if (song.Type.Any(x => x == SongType.Unknown))
            {
                throw new Exception("SongType is unknown");
            }

            if (song.Title.Length > 89)
            {
                throw new Exception($"Title is too long: {song.Title}");
            }

            if (!song.Title.Any())
            {
                // Console.WriteLine($"Title is empty: {song.Title}");
                throw new Exception($"Title is empty: {song.Title}");
            }

            if (song.AfterTitle.Length > 178) // should be like 20-30
            {
                throw new Exception($"AfterTitle is too long: {song.AfterTitle}");
            }

            if (song.AfterTitle.Contains('"'))
            {
                Console.WriteLine($"AfterTitle contains a quote: {song.AfterTitle}");
                throw new Exception($"AfterTitle contains a quote: {song.AfterTitle}");
            }

            if (song.AfterTitle.ToLowerInvariant().Contains(" op") ||
                song.AfterTitle.ToLowerInvariant().Contains(" ed") ||
                song.AfterTitle.ToLowerInvariant().Contains(" insert")) // todo
            {
                Console.WriteLine($"AfterTitle contains new songs: {song.AfterTitle}");
                throw new Exception($"AfterTitle contains new songs: {song.AfterTitle}");
            }
        }
    }

    private static IEnumerable<int> AllIndexesOf(this string str, string searchString)
    {
        int minIndex = str.IndexOf(searchString, StringComparison.OrdinalIgnoreCase);
        while (minIndex != -1)
        {
            yield return minIndex;
            minIndex = str.IndexOf(searchString, minIndex + searchString.Length,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

public class ParsedSong
{
    public string BeforeType { get; set; } = "";

    public List<SongType> Type { get; set; } = new();

    public string Title { get; set; } = "";

    public string AfterTitle { get; set; } = "";
}

public enum SongType
{
    Unknown,
    OP,
    ED,
    Insert,
    BGM,
}

public enum Mode
{
    BeforeSongType,
    SongType,
    SongTitle,
    AfterSongTitle,
}
