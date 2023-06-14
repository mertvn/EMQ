using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Server.Db.Imports;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VNDBStaffNotesParser;
using JsonSerializer = System.Text.Json.JsonSerializer;

// ReSharper disable StringLiteralTypo

namespace Tests;

public class VNDBStaffNotesParserTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test_Batch()
    {
        // @formatter:off
        List<string> blacklist = new()
        {
          "v24208", "v33175", "v19843", "v8664", // possible room for improvement
          "v15495", "v20070", "v20071", // too irregular
        };

        List<string> graylist = new()
        {
          "v1515", "v2368", "v2527", "v4054" // AfterTitle contains quote
        };
        // @formatter:on

        string date = Constants.ImportDateVndb;
        string folder = $"C:\\emq\\vndb\\{date}";
        var musicJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ music.json"))!;

        var parsedSongs = new List<ParsedSong>();
        var processedMusics = new List<ProcessedMusic>();
        foreach (dynamic dynData in musicJson)
        {
            string vnid = (string)dynData.VNID;
            Console.WriteLine($"VNID: {dynData.VNID}");

            if (blacklist.Contains(vnid))
            {
                Console.WriteLine($"Skipping blacklisted vnid {vnid}");
                continue;
            }

            string staffNotes = (string)dynData.MusicName;
            Console.WriteLine($"staffNotes: {staffNotes}");
            var actual = VNDBStaffNotesParser.Program.Parse(staffNotes, !graylist.Contains(vnid));
            parsedSongs.AddRange(actual);

            foreach (ParsedSong parsedSong in actual)
            {
                var processedMusic = new ProcessedMusic
                {
                    VNID = dynData.VNID,
                    title = dynData.title,
                    StaffID = dynData.StaffID,
                    ArtistAliasID = dynData.ArtistAliasID,
                    name = dynData.name,
                    role = dynData.role,
                    ParsedSong = parsedSong,
                    ProducerIds = (JArray.Parse((string)(dynData.ProducerIds.ToString()))).ToObject<string[]>()!
                        .Distinct().ToArray(),
                };
                processedMusics.Add(processedMusic);
            }
        }

        await File.WriteAllTextAsync($"{folder}\\processedMusics.json", JsonConvert.SerializeObject(processedMusics));
        Console.WriteLine("finished processing - count: " + parsedSongs.Count);
    }

    [Test]
    public void Test_Single()
    {
        string input = "ED \"Twinkle Snow\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Twinkle Snow",
                AfterTitle = ""
            }
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithDoubleSpaceBeforeTitle()
    {
        string input = "Starry☆Sky～Summer Stories～ OP  \"Sea-staR\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "Starry☆Sky～Summer Stories～ ",
                Type = new List<SongType> { SongType.OP },
                Title = "Sea-staR",
                AfterTitle = ""
            }
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_WeirdShitThatIDontKnowIfWeShouldAccept()
    {
        string input = "\"Kuroi Hitomi no Aria\", \"Sie Null\", ED \"DON'T LET GO\" [Remake]";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "\"Kuroi Hitomi no Aria\", \"Sie Null\", ",
                Type = new List<SongType> { SongType.ED },
                Title = "DON'T LET GO",
                AfterTitle = " [Remake]"
            }
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_WeirdQuotes()
    {
        string input = "ED “Negai”";

        var expected = new List<ParsedSong>
        {
            new() { BeforeType = "", Type = new List<SongType> { SongType.ED }, Title = "Negai", AfterTitle = "" }
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_PartialSuccess()
    {
        string input = "Nene ED \"Re:Start ~Kimi to Mata Deaete~\",Band performance \"Without You\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "Nene ",
                Type = new List<SongType> { SongType.ED },
                Title = "Re:Start ~Kimi to Mata Deaete~",
                AfterTitle = ""
            }
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_EndingED()
    {
        string input = "Grand Ending ED \"Kokuin ~Tattoo~\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "Grand ", // whatever
                Type = new List<SongType> { SongType.ED },
                Title = "Kokuin ~Tattoo~",
                AfterTitle = ""
            }
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_NoSongType()
    {
        string input = "\"Passion\"";

        var expected = new List<ParsedSong> { };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithBeforeSongTypeAndAfterTitle()
    {
        string input = "Shuusuke ED \"Hohoemi Genocide\" (credited as Alex3)";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "Shuusuke ",
                Type = new List<SongType> { SongType.ED },
                Title = "Hohoemi Genocide",
                AfterTitle = " (credited as Alex3)"
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesSpaceDelimiter()
    {
        string input = "ED1 \"Saya no Uta\" ED2 \"Garasu no Kutsu\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Saya no Uta",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Garasu no Kutsu",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesSpaceDelimiter2()
    {
        string input = "OP \"Hyouketsu no Yoru\" Insert song “Komori Uta”";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.OP },
                Title = "Hyouketsu no Yoru",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Komori Uta",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesCommaAndSpaceDelimiterIntoBeforeType()
    {
        string input =
            "OP \"Mirage Lullaby\", ED \"SCRAMBLE!\", PS2 OP \"ORIGINAL!\" Essence OP \"Link-age\", ED \"Summer Again\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.OP },
                Title = "Mirage Lullaby",
                AfterTitle = ""
            },
            new() { BeforeType = "", Type = new List<SongType> { SongType.ED }, Title = "SCRAMBLE!", AfterTitle = "" },
            new()
            {
                BeforeType = "PS2 ", Type = new List<SongType> { SongType.OP }, Title = "ORIGINAL!", AfterTitle = ""
            },
            new()
            {
                BeforeType = "Essence ",
                Type = new List<SongType> { SongType.OP },
                Title = "Link-age",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "", Type = new List<SongType> { SongType.ED }, Title = "Summer Again", AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesCommaAndSpaceDelimiterIntoBeforeType2()
    {
        string input =
            "OP \"Kuuki Rikigaku Shoujo to Shounen no Uta\" Looking-glass Insects ED \"Shuumatsu no Bishou\" Tsui no Sora II ED \"Norowareta Sei / Shukufukusareta Sei\", Knockin' on heaven's door ED \"Kuuki Rikigaku Shoujo to Shounen no Uta -PianoVocalVer-\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.OP },
                Title = "Kuuki Rikigaku Shoujo to Shounen no Uta",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "Looking-glass Insects ",
                Type = new List<SongType> { SongType.ED },
                Title = "Shuumatsu no Bishou",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "Tsui no Sora II ",
                Type = new List<SongType> { SongType.ED },
                Title = "Norowareta Sei / Shukufukusareta Sei",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "Knockin' on heaven's door ",
                Type = new List<SongType> { SongType.ED },
                Title = "Kuuki Rikigaku Shoujo to Shounen no Uta -PianoVocalVer-",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesCommaDelimiter()
    {
        string input = "OP \"Todokanai Koi\", Insert Song \"After All ~Tsuzuru Omoi~\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.OP },
                Title = "Todokanai Koi",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "After All ~Tsuzuru Omoi~",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test, Explicit]
    public void Test_MultipleWithDifferentSongTypesOxfordCommaAmpersandDelimiter()
    {
        // string _ =
        //     "OP \"Aoiro Step\", insert songs \"Heroine Mode\", \"Sweet Mission\", & \"Seishun Meiro de Syalalala\""; // todo
        string input =
            "OP \"AI CATCH\", ED themes \"De SLASH\", \"Only finally there is the free end\", \"PARA MIDIA\", and \"Masculine Devil\"";

        var expected = new List<ParsedSong>
        {
            new() { BeforeType = "", Type = new List<SongType> { SongType.OP }, Title = "AI CATCH", AfterTitle = "" },
            new() { BeforeType = "", Type = new List<SongType> { SongType.ED }, Title = "De SLASH", AfterTitle = "" },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Only finally there is the free end",
                AfterTitle = ""
            },
            new() { BeforeType = "", Type = new List<SongType> { SongType.ED }, Title = "PARA MIDIA", AfterTitle = "" },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Masculine Devil",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesCommaAndAndDelimiters()
    {
        string input =
            "OP \"Momiji\", Insert song \"Fuji no Tobari to Yoru no Uta\" and ED \"Ashita wo Egaku Omoi no Iro\"";

        var expected = new List<ParsedSong>
        {
            new() { BeforeType = "", Type = new List<SongType> { SongType.OP }, Title = "Momiji", AfterTitle = "" },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Fuji no Tobari to Yoru no Uta",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Ashita wo Egaku Omoi no Iro",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithDifferentSongTypesAndDelimiterWithDoubleSpaceBefore()
    {
        string input =
            "OP \"Harukaze ni Kiss Shite\"  and ED \"Ano Natsu o Wasurenai\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.OP },
                Title = "Harukaze ni Kiss Shite",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Ano Natsu o Wasurenai",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithSameSongTypesAndDelimiter()
    {
        string input =
            "PC OP \"HOLY WORLD\" and PS2 insert songs \"Kishin Houkou! Demonbane!\" and \"Evil Shine\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "PC ",
                Type = new List<SongType> { SongType.OP },
                Title = "HOLY WORLD",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "PS2 ",
                Type = new List<SongType> { SongType.Insert },
                Title = "Kishin Houkou! Demonbane!",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Evil Shine",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithSameSongTypesCommaDelimiter()
    {
        string input =
            "Insert Songs \"Shin'ai\", \"WHITE ALBUM\", \"SOUND OF DESTINY\", \"Todokanai Koi -live at campus Fes-\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "", Type = new List<SongType> { SongType.Insert }, Title = "Shin'ai", AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "WHITE ALBUM",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "SOUND OF DESTINY",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Todokanai Koi -live at campus Fes-",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithSameSongTypesSpaceDelimiter()
    {
        string input =
            "Insert songs \"Amazing Grace\" \"Ave Maria\" \"Joy to the World\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Amazing Grace",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Ave Maria",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Joy to the World",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithMultipleSongTypesCommaDelimiter()
    {
        string input = "BGM, ED \"Kono Michi de...\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED }, // YOLO
                Title = "Kono Michi de...",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithMultipleSongTypesAmpersandDelimiter()
    {
        string input = "OP & ED \"Present\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.OP, SongType.ED },
                Title = "Present",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithMultipleSongTypesForwardSlashDelimiter()
    {
        string input = "Insert song / ED \"eternal twinkle\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type =
                    new List<SongType> { SongType.Insert, SongType.ED },
                Title = "eternal twinkle",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithMultipleSongTypesForwardSlashDelimiterNoSpace()
    {
        string input = "OP/ED \"Suna no Shiro\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type =
                    new List<SongType> { SongType.OP, SongType.ED },
                Title = "Suna no Shiro",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test, Explicit]
    public void Test_SingleWithMultipleSongTypesForwardSlashDelimiterIntoBeforeType()
    {
        string input = "DC/PS2 OP/Kasumi ED \"Flow\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "DC/PS2 ",
                Type = new List<SongType> { SongType.OP, SongType.ED }, // idk if we should even bother with this one
                Title = "Flow",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_MultipleWithMultipleSongTypesAmpersandDelimiter()
    {
        string input = "Shizuru route ED \"Koibumi\", Akane route ED & Lucia route Insert song \"Itsuwaranai Kimi e\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "Shizuru route ",
                Type = new List<SongType> { SongType.ED },
                Title = "Koibumi",
                AfterTitle = ""
            },
            new()
            {
                // todo? CAVEAT: Only the last BeforeType is kept
                BeforeType = "Lucia route ",
                Type = new List<SongType> { SongType.ED, SongType.Insert },
                Title = "Itsuwaranai Kimi e",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_SingleWithMultipleSongTypesAndDelimiter()
    {
        string input = "Grand OP and True End ED \"Eigou Shinri no Fermata\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "True ",
                Type = new List<SongType> { SongType.OP, SongType.ED },
                Title = "Eigou Shinri no Fermata",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }


    [Test]
    public void Test_ComplexShit1()
    {
        string input =
            "PC OP \"Muv-Luv\", Extra ED \"I will.\", Unlimited ED \"Harukanaru Chikyuu (Furusato) no Uta\", All-Ages ver. OP \"divergence\", Xbox 360 OP \"first pain\", PS3 OP \"LOVE STEP\"";

        var expected = new List<ParsedSong>
        {
            new() { BeforeType = "PC ", Type = new List<SongType> { SongType.OP }, Title = "Muv-Luv", AfterTitle = "" },
            new()
            {
                BeforeType = "Extra ", Type = new List<SongType> { SongType.ED }, Title = "I will.", AfterTitle = ""
            },
            new()
            {
                BeforeType = "Unlimited ",
                Type = new List<SongType> { SongType.ED },
                Title = "Harukanaru Chikyuu (Furusato) no Uta",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "All-Ages ver. ",
                Type = new List<SongType> { SongType.OP },
                Title = "divergence",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "Xbox 360 ",
                Type = new List<SongType> { SongType.OP },
                Title = "first pain",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "PS3 ", Type = new List<SongType> { SongType.OP }, Title = "LOVE STEP", AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_ComplexShit2WithNoSpaceAfterComma()
    {
        string input =
            "ED \"To aru Ryuu no Koi no Uta\", Insert songs \"No Answer\",\"Are You Happy?\",\"To Aru Ryuu no Kami no Shi\" (Chorus)";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "To aru Ryuu no Koi no Uta",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "", Type = new List<SongType> { SongType.Insert }, Title = "No Answer", AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "Are You Happy?",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.Insert },
                Title = "To Aru Ryuu no Kami no Shi",
                AfterTitle = " (Chorus)"
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Test]
    public void Test_ComplexShit3()
    {
        string input =
            "Acta est Fabula OP \"Gregorio\" & ED \"Über den Himmel\", Amantes amentes OP \"Jubilus\" & ED \"Sanctus\", Switch version OP \"Einsatz -zugabe-\"";

        var expected = new List<ParsedSong>
        {
            new()
            {
                BeforeType = "Acta est Fabula ",
                Type = new List<SongType> { SongType.OP },
                Title = "Gregorio",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "",
                Type = new List<SongType> { SongType.ED },
                Title = "Über den Himmel",
                AfterTitle = ""
            },
            new()
            {
                BeforeType = "Amantes amentes ",
                Type = new List<SongType> { SongType.OP },
                Title = "Jubilus",
                AfterTitle = ""
            },
            new() { BeforeType = "", Type = new List<SongType> { SongType.ED }, Title = "Sanctus", AfterTitle = "" },
            new()
            {
                BeforeType = "Switch version ",
                Type = new List<SongType> { SongType.OP },
                Title = "Einsatz -zugabe-",
                AfterTitle = ""
            },
        };

        var actual = VNDBStaffNotesParser.Program.Parse(input);
        Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }
}
