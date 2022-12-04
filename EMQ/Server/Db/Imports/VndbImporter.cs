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
    public static List<Song> Songs { get; } = new();

    public static List<dynamic> musicSourcesJson { get; set; } = null!;

    public static List<dynamic> musicSourcesTitlesJson { get; set; } = null!; // todo

    public static List<dynamic> artistsJson { get; set; } = null!;

    public static List<dynamic> artists_aliasesJson { get; set; } = null!;

    public static List<ProcessedMusic> processedMusicsJson { get; set; } = null!;

    public static async Task ImportVndbData()
    {
        Songs.Clear();
        string date = "2022-12-04";

        musicSourcesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"C:\\emq\\vndb\\EMQ music_source {date}.json"))!;

        musicSourcesTitlesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"C:\\emq\\vndb\\EMQ music_source_title {date}.json"))!;

        artistsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"C:\\emq\\vndb\\EMQ artist {date}.json"))!;

        artists_aliasesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"C:\\emq\\vndb\\EMQ artist_alias {date}.json"))!;

        processedMusicsJson = JsonConvert.DeserializeObject<List<ProcessedMusic>>(
            await File.ReadAllTextAsync($"C:\\emq\\vndb\\processedMusics {date}.json"))!;

        Songs.AddRange(ImportVndbDataInner(processedMusicsJson));

        foreach (Song song in Songs)
        {
            int mId = await DbManager.InsertSong(song);
            Console.WriteLine($"Inserted mId {mId}");
        }
    }

    private static List<Song> ImportVndbDataInner(List<ProcessedMusic> dataJson)
    {
        var songs = new List<Song>();

        foreach (ProcessedMusic dynData in dataJson)
        {
            // Console.WriteLine($"Processing {JsonConvert.SerializeObject(dynData)}");
            var dynMusicSource = musicSourcesJson.Find(x => x.id == dynData.VNID)!;
            try
            {
                dynamic? _ = dynMusicSource.id;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching music source found for {dynData.VNID}");
                throw;
            }

            List<dynamic> dynMusicSourceTitles =
                musicSourcesTitlesJson.FindAll(x => (x.id == dynData.VNID) && (bool)x.official);
            try
            {
                dynamic? _ = dynMusicSourceTitles.First().id;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching music source title found for {dynData.VNID}");
                throw;
            }

            var dynArtistAlias = artists_aliasesJson.Single(x => (int)x.aid == (int)dynData.ArtistAliasID);
            try
            {
                dynamic? _ = dynArtistAlias.aid;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching artist alias found for {dynData.VNID}");
                throw;
            }

            var dynArtist = artistsJson.Find(x => x.id == dynArtistAlias.id)!;
            try
            {
                dynamic? _ = dynArtist.aid;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching artist found for aid {dynArtistAlias.aid}");
                throw;
            }

            bool artistAliasIsMain = (int)dynArtist.aid == (int)dynArtistAlias.aid;

            // Console.WriteLine((string)dynData.role);
            SongArtistRole role = (string)dynData.role switch
            {
                "songs" => SongArtistRole.Vocals,
                "music" => SongArtistRole.Composer,
                "staff" => SongArtistRole.Staff,
                "translator" => SongArtistRole.Translator,
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

            SongArtist songArtist = new SongArtist()
            {
                VndbId = dynArtist.id,
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

            var existingSong = songs.LastOrDefault(x =>
                x.Sources.First().Links.First().Url.Contains((string)dynData.VNID) &&
                x.Titles.Any(y => y.LatinTitle == (string)dynData.ParsedSong.Title));

            if (existingSong is not null)
            {
                var existingSongExistingArtist =
                    existingSong.Artists.SingleOrDefault(z => z.VndbId == (string)dynArtist.id);
                if (existingSongExistingArtist is not null)
                {
                    // todo
                    continue;

                    // Console.WriteLine(
                    //     $"Adding new role ({dynData.role}) to existing artist ({(string)dynArtist.id}) for source ({dynData.VNID})");
                    // existingSongExistingArtist.Roles.Add(songArtist);
                }

                Console.WriteLine($"Adding new artist ({dynArtist.id}) to existing source ({dynData.VNID})");
                existingSong.Artists.Add(songArtist);
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

            var musicSourceTitles = new List<Title>();

            foreach (dynamic dynMusicSourceTitle in dynMusicSourceTitles)
            {
                string latinTitle;
                string? nonLatinTitle;
                if (string.IsNullOrEmpty((string)dynMusicSourceTitle.latin))
                {
                    latinTitle = dynMusicSourceTitle.title;
                    nonLatinTitle = null;
                }
                else
                {
                    latinTitle = dynMusicSourceTitle.latin;
                    nonLatinTitle = dynMusicSourceTitle.title;
                }

                // we don't want titles that are exactly same
                if (musicSourceTitles.Any(x => x.LatinTitle == latinTitle))
                {
                    continue;
                }

                var musicSourceTitle = new Title()
                {
                    LatinTitle = latinTitle,
                    NonLatinTitle = nonLatinTitle,
                    Language = dynMusicSourceTitle.lang,
                    IsMainTitle = latinTitle == (string)dynMusicSource.title
                };
                musicSourceTitles.Add(musicSourceTitle);
            }

            var song = new Song()
            {
                Type = SongType.Standard, // todo?
                Length = -1, // todo?
                Titles =
                    new List<Title>()
                    {
                        new Title()
                        {
                            LatinTitle = dynData.ParsedSong.Title, Language = "ja", IsMainTitle = true // todo language
                        },
                        // todo multiple song titles?
                    },
                Artists = new List<SongArtist> { songArtist },
                // todo song links
                Sources = new List<SongSource>()
                {
                    new SongSource()
                    {
                        AirDateStart = airDateStart,
                        SongType = (SongSourceSongType)(int)dynData.ParsedSong.Type.First(), // todo combine these types, and handle list somehow
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
                        Titles = musicSourceTitles,
                        // todo categories
                    },
                }
            };
            songs.Add(song);
        }

        return songs;
    }
}
