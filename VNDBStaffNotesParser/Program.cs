using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VNDBStaffNotesParser
{
    public static class Program
    {
        static void Main(string[] args)
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
                            "Openings",
                            "OP1",
                            "OP2",
                            "OP3",
                            "OP4",
                            "OP5",
                            "OP6",
                            "OP7",
                            "OP8",
                            "OP9"
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
                            "Endings",
                            "ED1",
                            "ED2",
                            "ED3",
                            "ED4",
                            "ED5",
                            "ED6",
                            "ED7",
                            "ED8",
                            "ED9"
                        }.OrderByDescending(x => x).ToList()
                    }
                },
                // TODO: check if there are any Insert1 etc.
                new Dictionary<SongType, List<string>>
                {
                    {
                        SongType.Insert, new List<string>
                        {
                            "Insert",
                            "Inserts",
                            "Insert Song",
                            "Insert Songs",
                            "Image song",
                            "Image songs",
                            "Interlude",
                        }.OrderByDescending(x => x).ToList()
                    }
                },
                new Dictionary<SongType, List<string>>
                {
                    { SongType.BGM, new List<string> { "BGM", }.OrderByDescending(x => x).ToList() }
                },
            };

        public static List<Song> Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("input is null or empty");
                return new List<Song>();
            }

            if (!IsProcessable(input))
            {
                Console.WriteLine($"Unprocessable input: {input}");
                return new List<Song>();
            }

            var songs = new List<Song>();

            Mode mode = Mode.SongType;
            int cursor = 0;
            var song = new Song();
            bool foundSongTypeAtStart = false;
            while (cursor < input.Length)
            {
                switch (mode)
                {
                    case Mode.BeforeSongType:
                        // TODO: Rewrite this to scan char by char searching for songTypeNames instead of enumerating the dicts
                        var possibleIndexOf = new List<int>();
                        foreach (Dictionary<SongType, List<string>> songTypeDict in SongTypeDicts)
                        {
                            foreach ((SongType _, List<string>? value) in songTypeDict)
                            {
                                foreach (string songTypeName in value)
                                {
                                    // require a space before because we don't want to match inside words
                                    var indexesOf = input.AllIndexesOf(" " + songTypeName).ToList();
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
                            Console.WriteLine("Skipping shit");
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
                                    string substr = input.Substring(cursor, songTypeName.Length);
                                    Console.WriteLine(substr + "==" + songTypeName);
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
                        if (input[cursor] != '"')
                        {
                            if (input[cursor] == '&') // multiple types for the same song
                            {
                                if (input[++cursor] == ' ')
                                {
                                    cursor += 1; // skip the space after '&' if there is one
                                }

                                mode = Mode.SongType;
                                goto nextMode;
                            }
                            else
                            {
                                throw new Exception("Invalid first char for SongTitle");
                            }
                        }

                        string songTitle = "";
                        while (input[++cursor] != '"')
                        {
                            songTitle += input[cursor];
                        }

                        song.Title = songTitle;

                        string serialized = JsonSerializer.Serialize(song);
                        Console.WriteLine("add " + serialized);
                        songs.Add(JsonSerializer.Deserialize<Song>(serialized)!);

                        int boundsCheck = ++cursor;
                        if (boundsCheck >= 0 && input.Length > boundsCheck)
                        {
                            // todo: abstractize this to reduce duplication
                            switch (input[boundsCheck])
                            {
                                // new song delimited by ','
                                case ',':
                                    {
                                        switch (input[boundsCheck + 2])
                                        {
                                            // new song with same song type
                                            case '"':
                                                cursor = boundsCheck + 2;
                                                mode = Mode.SongTitle;

                                                song = new Song { Type = song.Type };

                                                goto nextMode;
                                            // new song with different song type
                                            default:
                                                cursor = boundsCheck + 2; // todo titles with no space after comma?
                                                mode = Mode.SongType;

                                                song = new Song();

                                                goto nextMode;
                                        }
                                    }
                                // todo: breaks other stuff
                                // case ' ':
                                //     {
                                //         switch (input[boundsCheck + 1])
                                //         {
                                //             // new song with same song type
                                //             case '"':
                                //                 cursor = boundsCheck + 1;
                                //                 mode = Mode.SongTitle;
                                //
                                //                 song = new Song { Type = song.Type };
                                //
                                //                 goto nextMode;
                                //             // new song with different song type
                                //             default:
                                //                 cursor = boundsCheck + 1; // todo titles with no space after comma?
                                //                 mode = Mode.SongType;
                                //
                                //                 song = new Song();
                                //
                                //                 goto nextMode;
                                //         }
                                //
                                //         break;
                                //     }

                                // AfterTitle
                                default:
                                    {
                                        int boundsCheck3 = ++cursor;
                                        if (boundsCheck3 >= 0 && input.Length > boundsCheck3)
                                        {
                                            Console.WriteLine(input[boundsCheck3]);

                                            if (string.Equals(input.Substring(boundsCheck3, "and".Length), "and",
                                                    StringComparison.OrdinalIgnoreCase))
                                            {
                                                // new song delimited by 'and'
                                                switch (input[boundsCheck3 + 1 + "and".Length])
                                                {
                                                    // new song with same song type
                                                    case '"':
                                                        cursor = boundsCheck3 + 1 + "and".Length;
                                                        mode = Mode.SongTitle;

                                                        song = new Song { Type = song.Type };

                                                        goto nextMode;
                                                    // new song with different song type
                                                    default:
                                                        cursor = boundsCheck3 + 1 +
                                                                 "and".Length; // todo titles with no space after comma
                                                        mode = Mode.SongType;

                                                        song = new Song();

                                                        goto nextMode;
                                                }
                                            }
                                            else if (string.Equals(input.Substring(boundsCheck3, "&".Length), "&",
                                                         StringComparison.OrdinalIgnoreCase))
                                            {
                                                // new song delimited by 'and'
                                                switch (input[boundsCheck3 + 1 + "&".Length])
                                                {
                                                    // new song with same song type
                                                    case '"':
                                                        cursor = boundsCheck3 + 1 + "&".Length;
                                                        mode = Mode.SongTitle;

                                                        song = new Song { Type = song.Type };

                                                        goto nextMode;
                                                    // new song with different song type
                                                    default:
                                                        cursor = boundsCheck3 + 1 +
                                                                 "&".Length; // todo titles with no space after comma
                                                        mode = Mode.SongType;

                                                        song = new Song();

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
                                    // todo?
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

            Console.WriteLine(JsonSerializer.Serialize(songs,
                new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    Converters = { new JsonStringEnumConverter() }
                }));

            CheckIntegrity(songs);

            return songs;
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

            return true;
        }

        private static void CheckIntegrity(List<Song> songs)
        {
            foreach (Song song in songs)
            {
                if (song.BeforeType.Length > 50)
                {
                    throw new Exception($"BeforeType is too long: {song.BeforeType}");
                }

                if (song.Type.Any(x => x == SongType.Unknown))
                {
                    throw new Exception("SongType is unknown");
                }

                if (song.Title.Length > 50)
                {
                    throw new Exception($"Title is too long: {song.Title}");
                }

                if (song.AfterTitle.Length > 50)
                {
                    throw new Exception($"AfterTitle is too long: {song.AfterTitle}");
                }
            }
        }

        public static IEnumerable<int> AllIndexesOf(this string str, string searchString)
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

    public class Song
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
}
