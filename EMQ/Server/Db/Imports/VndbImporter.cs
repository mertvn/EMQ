using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;

namespace EMQ.Server.Db.Imports;

public static class VndbImporter
{
    public static List<dynamic> musicSourcesJson { get; set; }

    public static List<dynamic> artistsJson { get; set; }

    public static List<dynamic> artists_aliasesJson { get; set; }

    public static List<dynamic> opsJson { get; set; }

    public static List<dynamic> edsJson { get; set; }

    public static List<dynamic> insertsJson { get; set; }

    public static async Task ImportVndbData()
    {
        musicSourcesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync("C:\\emq\\vndb\\EMQ music_source 2022-10-31.json"))!;

        artistsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync("C:\\emq\\vndb\\EMQ artist 2022-10-31.json"))!;

        artists_aliasesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync("C:\\emq\\vndb\\EMQ artist_alias 2022-10-31.json"))!;

        opsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync("C:\\emq\\vndb\\EMQ OP 2022-10-31.json"))!;

        edsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync("C:\\emq\\vndb\\EMQ ED 2022-10-31.json"))!;

        insertsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync("C:\\emq\\vndb\\EMQ Insert 2022-10-31.json"))!;

        await VndbImporter.ImportVndbDataInner(opsJson, SongSourceSongType.OP);
        await VndbImporter.ImportVndbDataInner(edsJson, SongSourceSongType.ED);
        await VndbImporter.ImportVndbDataInner(insertsJson, SongSourceSongType.Insert);
    }

    private static async Task ImportVndbDataInner(List<dynamic> dataJson, SongSourceSongType songSourceSongType)
    {
        var songs = new List<Song>();

        foreach (dynamic dynData in dataJson)
        {
            bool processed = false;
            var dynMusicSource = musicSourcesJson.Find(x => x.id == dynData.VNID)!;
            if (dynMusicSource.id is null)
            {
                Console.WriteLine($"No matching music source found for {dynData.VNID}; skipping");
                return;
            }

            var songArtists = new List<SongArtist>();
            var dynArtists = artistsJson.FindAll(x => x.aid == dynData.ArtistAliasID)!;
            foreach (dynamic dynArtist in dynArtists)
            {
                if (dynArtist.id is null)
                {
                    Console.WriteLine($"No matching artist found for {dynData.VNID}; skipping");
                    return;
                }

                var dynArtistAlias = artists_aliasesJson.Find(x => x.aid == dynArtist.aid)!;
                if (dynArtistAlias.aid is null)
                {
                    Console.WriteLine($"No matching alias found for {dynArtist.id}; skipping");
                    return;
                }

                bool artistAliasIsMain = (int)dynArtist.aid == (int)dynArtistAlias.aid; // todo doesn't work

                // Console.WriteLine((string)dynOp.role);
                SongArtistRole role = (string)dynData.role switch
                {
                    "songs" => SongArtistRole.Vocals,
                    "music" => SongArtistRole.Composer,
                    _ => throw new Exception("Invalid artist role")
                };

                // Console.WriteLine((string)dynArtist.gender);
                Sex sex = (string)dynArtist.gender switch
                {
                    "f" => Sex.Female,
                    "m" => Sex.Male,
                    "unknown" => Sex.Unknown,
                    _ => throw new Exception("Invalid artist sex")
                };

                var songArtist = new SongArtist()
                {
                    Role = role,
                    PrimaryLanguage = dynArtist.lang,
                    Titles =
                        new List<Title>()
                        {
                            new Title()
                            {
                                LatinTitle = dynArtistAlias.name,
                                NonLatinTitle = dynArtistAlias.original,
                                Language = dynArtist.lang, // todo
                                IsMainTitle = artistAliasIsMain
                            },
                        },
                    Sex = sex
                };

                var existingSong = songs.Find(x => x.Sources.First().Links.First().Url.Contains((string)dynData.VNID));
                if (existingSong is not null)
                {
                    Console.WriteLine($"Adding new artist ({dynArtist.id}) to existing song source ({dynData.VNID})");
                    existingSong.Artists.Add(songArtist);
                    processed = true;
                    break;
                }
                else
                {
                    songArtists.Add(songArtist);
                }
            }

            if (processed)
            {
                continue;
            }

            // Why yes, I did have fun writing this
            int date = (int)dynMusicSource.air_date_start;
            if (date.ToString().EndsWith("9999"))
            {
                date -= 9898;
            }
            else if (date.ToString().EndsWith("99"))
            {
                date -= 98;
            }

            var airDateStart = DateTime.ParseExact(date.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);

            var song = new Song()
            {
                Type = SongType.Standard, // todo?
                Length = -1, // todo?
                Titles =
                    new List<Title>()
                    {
                        new Title()
                        {
                            LatinTitle = dynData.MusicName, Language = "ja", IsMainTitle = true // todo language
                        },
                        // todo multiple song titles?
                    },
                Artists = songArtists,
                // todo song links
                Sources = new List<SongSource>()
                {
                    new SongSource()
                    {
                        AirDateStart = airDateStart,
                        SongType = songSourceSongType,
                        LanguageOriginal = dynMusicSource.olang,
                        RatingAverage = dynMusicSource.c_average,
                        Type = SongSourceType.VN,
                        Links = new List<SongSourceLink>()
                        {
                            new SongSourceLink()
                            {
                                Type = SongSourceLinkType.VNDB, Url = "https://vndb.org/" + dynMusicSource.id
                            }
                        },
                        Titles =
                            new List<Title>()
                            {
                                new Title()
                                {
                                    LatinTitle = dynMusicSource.title,
                                    NonLatinTitle = dynMusicSource.original,
                                    Language = dynMusicSource.olang, // todo
                                    IsMainTitle = true
                                },
                                // todo multiple source titles
                            },
                        // todo categories
                    },
                }
            };
            songs.Add(song);
        }

        foreach (Song song in songs)
        {
            int mId = await DbManager.InsertSong(song);
            Console.WriteLine($"Inserted mId {mId}");
        }
    }
}
