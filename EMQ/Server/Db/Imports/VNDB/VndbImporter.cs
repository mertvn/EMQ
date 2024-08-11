using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dapper;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Newtonsoft.Json;
using Npgsql;

namespace EMQ.Server.Db.Imports.VNDB;

public static class VndbImporter
{
    public static List<Song> PendingSongs { get; set; } = new();

    public static List<dynamic> musicSourcesJson { get; set; } = null!;

    public static List<dynamic> musicSourcesTitlesJson { get; set; } = null!;

    public static List<dynamic> artistsJson { get; set; } = null!;

    public static List<dynamic> artists_aliasesJson { get; set; } = null!;

    public static List<ProcessedMusic> processedMusicsJson { get; set; } = null!;

    public static List<VNTagInfo> vnTagInfoJson { get; set; } = null!;

    public static List<Tag> tagsJson { get; set; } = null!;

    public static async Task ImportVndbData(DateTime dateTime, bool isIncremental)
    {
        if (!isIncremental && ConnectionHelper.GetConnectionString().Contains("erogemusicquiz.com"))
        {
            throw new Exception("wrong db");
        }

        if (ConnectionHelper.GetConnectionString().Contains("AUTH"))
        {
            throw new Exception("wrong db");
        }

        PendingSongs.Clear();
        string date = dateTime.ToString("yyyy-MM-dd");
        string folder;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            folder = $"C:/emq/vndb/{date}";
        }
        else
        {
            folder = $"vndbimporter/{date}";
            Console.WriteLine($"{Directory.GetCurrentDirectory()}/{folder}");
        }

        musicSourcesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}/EMQ music_source.json"))!;

        musicSourcesTitlesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}/EMQ music_source_title.json"))!;

        artistsJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}/EMQ artist.json"))!;

        artists_aliasesJson = JsonConvert.DeserializeObject<List<dynamic>>(
            await File.ReadAllTextAsync($"{folder}/EMQ artist_alias.json"))!;

        processedMusicsJson = JsonConvert.DeserializeObject<List<ProcessedMusic>>(
            await File.ReadAllTextAsync($"{folder}/processedMusics.json"))!;

        vnTagInfoJson = JsonConvert.DeserializeObject<List<VNTagInfo>>(
            await File.ReadAllTextAsync($"{folder}/EMQ vnTagInfo.json"))!;

        tagsJson = JsonConvert.DeserializeObject<List<Tag>>(
            await File.ReadAllTextAsync($"{folder}/EMQ tags.json"))!;

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

        var incomingSongs = ImportVndbDataInner(processedMusicsJson);
        await File.WriteAllTextAsync($"{folder}/VndbImporter.json",
            System.Text.Json.JsonSerializer.Serialize(incomingSongs, Utils.Jso));

        List<Song> dbSongs;
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
        {
            const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
            Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
                .GroupBy(x => x.Item1)
                .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

            List<int> validMids = mids
                .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
                .Select(z => z.Key)
                .ToList();

            dbSongs = await DbManager.SelectSongsMIds(validMids.ToArray(), false);
        }

        HashSet<string> dbHashes = dbSongs
            .Select(x => x.ToSongLite())
            .Select(y => y.EMQSongHash).ToHashSet();

        HashSet<string> incomingHashes = incomingSongs
            .Select(x => x.ToSongLite())
            .Select(y => y.EMQSongHash).ToHashSet();

        foreach (Song song in dbSongs)
        {
            var songLite = song.ToSongLite();
            string hash = songLite.EMQSongHash;

            bool isInIncoming = incomingHashes.Contains(hash);
            if (!isInIncoming)
            {
                Console.WriteLine($"dbSong was not found in incoming: {song}");
            }
        }

        List<Song> canInsertDirectly = new();
        List<Song> canNotInsertDirectly = new();
        foreach (Song song in incomingSongs)
        {
            var songLite = song.ToSongLite();
            string hash = songLite.EMQSongHash;

            bool isInDb = dbHashes.Contains(hash);
            if (!isInDb)
            {
                Console.WriteLine($"new/modified song: {song}");
                if (!isIncremental)
                {
                    int _ = await DbManager.InsertSong(song);
                }
                else
                {
                    bool isSingleSource = song.Sources.Count == 1;
                    if (isSingleSource)
                    {
                        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                        string url = song.Sources.First().Links.Single(x => x.Type == SongSourceLinkType.VNDB).Url;
                        bool sourceExists = await connection.ExecuteScalarAsync<bool>(
                            "SELECT 1 FROM music_source_external_link where url = @url", new { url });
                        if (!sourceExists)
                        {
                            canInsertDirectly.Add(song);
                            continue;
                        }
                    }

                    canNotInsertDirectly.Add(song);
                }
            }
            else
            {
                // todo update source and artist data
                // Console.WriteLine($"skipping existing song: {song}");
            }
        }

        PendingSongs.AddRange(canNotInsertDirectly);

        {
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            foreach (Song song in canInsertDirectly)
            {
                Console.WriteLine($"inserting non-existing-source song: {song}");
                int _ = await DbManager.InsertSong(song, connection, transaction);
            }

            await transaction.CommitAsync();
        }

        foreach (SongSource songSource in incomingSongs.SelectMany(song => song.Sources))
        {
            songSource.Categories = new List<SongSourceCategory>();
        }

        await File.WriteAllTextAsync($"{folder}/VndbImporter_no_categories.json",
            System.Text.Json.JsonSerializer.Serialize(incomingSongs, Utils.Jso));

        Console.WriteLine("VndbImporter is done.");
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
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
                dynamic? _ = dynArtist.main;
            }
            catch (Exception)
            {
                Console.WriteLine($"No matching artist found for main {dynArtistAlias.main}");
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
                // Console.WriteLine(
                //     JsonConvert.SerializeObject(dynData.VNID +
                //                                 $" has no tags: {JsonConvert.SerializeObject(vnTagInfo)}"));
            }

            // Console.WriteLine(JsonConvert.SerializeObject(tags, Formatting.Indented));

            bool artistAliasIsMain = (int)dynArtist.main == (int)dynArtistAlias.aid;

            // Console.WriteLine(dynData.role);
            SongArtistRole role = dynData.role switch
            {
                "songs" => SongArtistRole.Vocals,
                "music" => SongArtistRole.Composer,
                "staff" => SongArtistRole.Staff,
                "translator" => SongArtistRole.Translator,
                _ => throw new Exception($"Invalid artist role: {dynArtist.role}")
            };

            // Console.WriteLine((string)dynArtist.gender);
            Sex sex = (string)dynArtist.gender switch
            {
                "f" => Sex.Female,
                "m" => Sex.Male,
                "" => Sex.Unknown,
                _ => throw new Exception($"Invalid artist sex: {(string)dynArtist.gender}")
            };

            (string artistLatinTitle, string? artistNonLatinTitle) = VndbTitleToEmqTitle((string)dynArtistAlias.name,
                (string?)dynArtistAlias.latin);

            SongArtist songArtist = new SongArtist()
            {
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

            songArtist.Links.Add(new SongArtistLink
            {
                Url = ((string)dynArtist.id).ToVndbUrl(),
                Type = SongArtistLinkType.VNDBStaff,
                Name = "",
            });

            var existingSong = songs.LastOrDefault(x =>
                x.Sources.Any(y =>
                    y.Links.Single(z => z.Type == SongSourceLinkType.VNDB).Url.Contains(dynData.VNID)) &&
                x.Titles.Any(y => string.Equals(y.LatinTitle,
                    dynData.ParsedSong.Title, StringComparison.OrdinalIgnoreCase)) &&
                x.Sources.SelectMany(y => y.SongTypes).Any(z =>
                    dynData.ParsedSong.Type.Select(st => (int)st).Cast<SongSourceSongType>().Contains(z)));

            if (existingSong is not null)
            {
                bool isBlacklisted = Blacklists.VndbImporterExistingSongBlacklist.Any(x =>
                    x.Item1 == dynData.VNID &&
                    string.Equals(x.Item2, dynData.ParsedSong.Title, StringComparison.InvariantCultureIgnoreCase));

                if (isBlacklisted)
                {
                    Console.WriteLine($"Blacklisted existing song: {dynData.VNID} {dynData.ParsedSong.Title}");
                }
                else
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
                // if (musicSourceTitles.Any(x =>
                //         string.Equals(x.LatinTitle, latinTitle, StringComparison.OrdinalIgnoreCase) &&
                //         (string.IsNullOrWhiteSpace(nonLatinTitle) || string.Equals(x.NonLatinTitle, nonLatinTitle,
                //             StringComparison.OrdinalIgnoreCase))))
                // {
                //     continue;
                // }

                var musicSourceTitle = new Title()
                {
                    LatinTitle = latinTitle,
                    NonLatinTitle = nonLatinTitle,
                    Language = dynMusicSourceTitle.lang,
                    // IsMainTitle = string.Equals(latinTitle, (string)dynMusicSource.title,
                    //                   StringComparison.OrdinalIgnoreCase) &&
                    //               string.Equals((string)dynMusicSourceTitle.lang, (string)dynMusicSource.olang,
                    //                   StringComparison.OrdinalIgnoreCase)
                    IsMainTitle = string.Equals((string)dynMusicSourceTitle.lang, (string)dynMusicSource.olang,
                        StringComparison.OrdinalIgnoreCase)
                };
                musicSourceTitles.Add(musicSourceTitle);
            }

            if (!musicSourceTitles.Any(x => x.IsMainTitle))
            {
                throw new Exception();
            }

            var song = new Song()
            {
                Type = SongType.Standard, // todo?
                DataSource = DataSourceKind.VNDB,
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
                        // Popularity = dynMusicSource.c_popularity,
                        VoteCount = dynMusicSource.c_votecount,
                        Type = SongSourceType.VN,
                        Links = new List<SongSourceLink>()
                        {
                            new SongSourceLink()
                            {
                                Type = SongSourceLinkType.VNDB,
                                Url = ((string)dynMusicSource.id).ToVndbUrl(),
                                Name = musicSourceTitles.First(x => x.IsMainTitle).ToString(),
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

            // if (song.Titles.Any(x=> x.LatinTitle == "Unmei -SADAME-"))
            // {
            //     Console.WriteLine("here");
            // }

            if (sameSong is not null)
            {
                Console.WriteLine(
                    $"Same song! {dynData.VNID} <-> {sameSong.Sources.First().Titles.First().LatinTitle}");
                sameSong.Sources.AddRange(song.Sources.Except(sameSong.Sources));
            }
            else
            {
                songs.Add(song);
            }
        }

        return songs;
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
}
