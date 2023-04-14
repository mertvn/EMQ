using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;

namespace EMQ.Server.Db.Imports.VNDB;

public static class VndbImporter
{
    public static List<Song> Songs { get; } = new();

    public static List<dynamic> musicSourcesJson { get; set; } = null!;

    public static List<dynamic> musicSourcesTitlesJson { get; set; } = null!;

    public static List<dynamic> artistsJson { get; set; } = null!;

    public static List<dynamic> artists_aliasesJson { get; set; } = null!;

    public static List<ProcessedMusic> processedMusicsJson { get; set; } = null!;

    public static List<VNTagInfo> vnTagInfoJson { get; set; } = null!;

    public static List<Tag> tagsJson { get; set; } = null!;

    public static async Task ImportVndbData()
    {
        Songs.Clear();
        string date = Constants.ImportDateVndb;
        string folder = $"C:\\emq\\vndb\\{date}";

        musicSourcesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ music_source.json"))!;

        musicSourcesTitlesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ music_source_title.json"))!;

        artistsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ artist.json"))!;

        artists_aliasesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ artist_alias.json"))!;

        processedMusicsJson = JsonConvert.DeserializeObject<List<ProcessedMusic>>(
            await File.ReadAllTextAsync($"{folder}\\processedMusics.json"))!;

        vnTagInfoJson = JsonConvert.DeserializeObject<List<VNTagInfo>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ vnTagInfo.json"))!;

        tagsJson = JsonConvert.DeserializeObject<List<Tag>>(
            await File.ReadAllTextAsync($"{folder}\\EMQ tags.json"))!;

        var hashSetAId = new HashSet<int>();
        foreach (dynamic dyn in artists_aliasesJson)
        {
            int? aId = (int?)dyn.aid;
            if (!hashSetAId.Add(aId.Value))
            {
                throw new Exception();
            }
        }

        var hashSetTagId = new HashSet<string>();
        foreach (var dyn in tagsJson)
        {
            if (!hashSetTagId.Add(dyn.Id))
            {
                throw new Exception();
            }
        }

        Songs.AddRange(ImportVndbDataInner(processedMusicsJson));

        await File.WriteAllTextAsync("C:\\emq\\emqsongsmetadata\\VndbImporter.json",
            System.Text.Json.JsonSerializer.Serialize(Songs, Utils.Jso));

        foreach (Song song in Songs)
        {
            int mId = await DbManager.InsertSong(song);
            Console.WriteLine($"Inserted mId {mId}");
        }

        foreach (SongSource songSource in Songs.SelectMany(song => song.Sources))
        {
            songSource.Categories = new List<SongSourceCategory>();
        }

        await File.WriteAllTextAsync("C:\\emq\\emqsongsmetadata\\VndbImporter_no_categories.json",
            System.Text.Json.JsonSerializer.Serialize(Songs, Utils.Jso));
    }

    private static List<Song> ImportVndbDataInner(List<ProcessedMusic> dataJson)
    {
        var songs = new List<Song>();

        var musicSourcesDict = musicSourcesJson.ToDictionary(x => (string)x.id);
        var musicSourcesTitlesLookup = musicSourcesTitlesJson.ToLookup(x => (string)x.id);
        var artists_aliasesDict = artists_aliasesJson.ToDictionary(x => (int)x.aid);
        var tagsDict = tagsJson.ToDictionary(x => x.Id);

        foreach (ProcessedMusic dynData in dataJson)
        {
            // Console.WriteLine($"Processing {JsonConvert.SerializeObject(dynData)}");
            dynamic dynMusicSource = musicSourcesDict[dynData.VNID];
            try
            {
                dynamic? _ = dynMusicSource.id;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching music source found for {dynData.VNID}");
                throw;
            }

            List<dynamic> dynMusicSourceTitles = musicSourcesTitlesLookup[dynData.VNID].ToList();
            try
            {
                dynamic? _ = dynMusicSourceTitles.First().id;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching music source title found for {dynData.VNID}");
                throw;
            }

            dynamic dynArtistAlias = artists_aliasesDict[dynData.ArtistAliasID];
            try
            {
                dynamic? _ = dynArtistAlias.aid;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching artist alias found for {dynData.VNID}");
                throw;
            }

            dynamic dynArtist = artistsJson.Find(x => x.id == dynArtistAlias.id)!;
            try
            {
                dynamic? _ = dynArtist.aid;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching artist found for aid {dynArtistAlias.aid}");
                throw;
            }

            List<SongSourceCategory> categories = new();

            VNTagInfo? vnTagInfo = vnTagInfoJson.SingleOrDefault(x => x.VNID == dynData.VNID)!;
            if (vnTagInfo != null! && vnTagInfo.TVIs.Any())
            {
                foreach (var tvi in vnTagInfo.TVIs)
                {
                    var tag = tagsDict[tvi.t];
                    // Console.WriteLine(JsonConvert.SerializeObject(tag));

                    categories.Add(new SongSourceCategory
                    {
                        Name = tag.Name,
                        VndbId = tag.Id,
                        Type = SongSourceCategoryType.Tag,
                        Rating = tvi.r,
                        SpoilerLevel = (SpoilerLevel)tvi.s
                    });
                }
            }
            else
            {
                Console.WriteLine(
                    JsonConvert.SerializeObject(dynData.VNID +
                                                $" has no tags: {JsonConvert.SerializeObject(vnTagInfo)}"));
            }

            // Console.WriteLine(JsonConvert.SerializeObject(tags, Formatting.Indented));

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

            (string artistLatinTitle, string? artistNonLatinTitle) = VndbTitleToEmqTitle((string)dynArtistAlias.name,
                (string?)dynArtistAlias.latin);

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
                            LatinTitle = artistLatinTitle,
                            NonLatinTitle = artistNonLatinTitle,
                            Language = dynArtist.lang, // todo
                            IsMainTitle = artistAliasIsMain
                        },
                    },
                Sex = sex
            };

            var existingSong = songs.LastOrDefault(x =>
                x.Sources.Any(y =>
                    y.Links.Single(z => z.Type == SongSourceLinkType.VNDB).Url.Contains((string)dynData.VNID)) &&
                x.Titles.Any(y => string.Equals(y.LatinTitle.ToLowerInvariant(),
                    (string)dynData.ParsedSong.Title.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));

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

                // Console.WriteLine($"Adding new artist ({dynArtist.id}) to existing source ({dynData.VNID})");
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
                if (!(bool)dynMusicSourceTitle.official)
                {
                    continue;
                }

                (string latinTitle, string? nonLatinTitle) = VndbTitleToEmqTitle((string)dynMusicSourceTitle.title,
                    (string?)dynMusicSourceTitle.latin);

                // we don't want titles that are exactly the same
                if (musicSourceTitles.Any(x =>
                        string.Equals(x.LatinTitle, latinTitle, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrWhiteSpace(nonLatinTitle) || string.Equals(x.NonLatinTitle, nonLatinTitle,
                            StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                var musicSourceTitle = new Title()
                {
                    LatinTitle = latinTitle,
                    NonLatinTitle = nonLatinTitle,
                    Language = dynMusicSourceTitle.lang,
                    IsMainTitle = string.Equals(latinTitle, (string)dynMusicSource.title,
                        StringComparison.OrdinalIgnoreCase)
                };
                musicSourceTitles.Add(musicSourceTitle);
            }

            var song = new Song()
            {
                Type = SongType.Standard, // todo?
                Titles =
                    new List<Title>()
                    {
                        new Title()
                        {
                            LatinTitle = dynData.ParsedSong.Title, Language = "ja", IsMainTitle = true // todo language
                        },
                    }.OrderBy(y => y).ToList(),
                Artists = new List<SongArtist> { songArtist }.OrderBy(y => y).ToList(),
                Sources = new List<SongSource>()
                {
                    new SongSource()
                    {
                        AirDateStart = airDateStart,
                        SongTypes = dynData.ParsedSong.Type.Select(x => (int)x).Cast<SongSourceSongType>().ToList(),
                        LanguageOriginal = dynMusicSource.olang,
                        RatingAverage = dynMusicSource.c_average,
                        RatingBayesian = dynMusicSource.c_rating,
                        Popularity = dynMusicSource.c_popularity,
                        VoteCount = dynMusicSource.c_votecount,
                        Type = SongSourceType.VN,
                        Links = new List<SongSourceLink>()
                        {
                            new SongSourceLink()
                            {
                                Type = SongSourceLinkType.VNDB, Url = ((string)dynMusicSource.id).ToVndbUrl()
                            }
                        },
                        Titles = musicSourceTitles,
                        Categories = categories,
                    },
                },
                ProducerIds = dynData.ProducerIds.OrderBy(y => y).ToList()
            };

            var sameSong = songs.SingleOrDefault(x =>
                x.Artists.Any(y => song.Artists.Select(z => z.VndbId).Contains(y.VndbId)) &&
                x.Titles.Any(y =>
                    song.Titles.Select(z => z.LatinTitle.ToLowerInvariant())
                        .Contains(y.LatinTitle.ToLowerInvariant())) &&
                x.ProducerIds.Any(y => song.ProducerIds.Contains(y)));

            if (sameSong is not null)
            {
                Console.WriteLine(
                    $"Same song! {dynData.title} <-> {sameSong.Sources.First().Titles.First().LatinTitle}");
                sameSong.Sources.AddRange(song.Sources.Except(sameSong.Sources));
            }
            else
            {
                songs.Add(song);
            }
        }

        return songs;
    }

    private static (string latinTitle, string? nonLatinTitle) VndbTitleToEmqTitle(string vndbName, string? vndbLatin)
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
}
