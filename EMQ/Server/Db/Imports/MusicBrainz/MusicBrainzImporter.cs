using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.MusicBrainz.Model;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Imports.MusicBrainz;

public class MusicBrainzVndbArtistJson
{
    public Guid mb { get; set; } = Guid.Empty;

    public string[] vndbid { get; set; } = Array.Empty<string>();
}

public static class MusicBrainzImporter
{
    public static List<Song> Songs { get; } = new();

    public static Dictionary<Guid, string[]> MusicBrainzVndbArtistDict { get; set; } = new();

    public static async Task ImportMusicBrainzData()
    {
        if (ConnectionHelper.GetConnectionString().Contains("railway"))
        {
            throw new Exception("wrong db");
        }

        Songs.Clear();
        string date = Constants.ImportDateMusicBrainz;
        string folder = $"C:\\emq\\musicbrainz\\{date}";
        string file = await File.ReadAllTextAsync($"{folder}/musicbrainz.json");
        var json = JsonSerializer.Deserialize<MusicBrainzJson[]>(file);

        string file1 = await File.ReadAllTextAsync($"{folder}/musicbrainz_vndb_artist.json");
        var json1 = JsonSerializer.Deserialize<MusicBrainzVndbArtistJson[]>(file1);
        MusicBrainzVndbArtistDict = json1!.ToDictionary(x => x.mb, x => x.vndbid);

        Songs.AddRange(await ImportMusicBrainzDataInner(json!));

        if (Songs.DistinctBy(x => x.MusicBrainzRecordingGid).Count() != Songs.Count)
        {
            throw new Exception("duplicate recordings detected");
        }

        await File.WriteAllTextAsync("C:\\emq\\emqsongsmetadata\\MusicBrainzImporter.json",
            System.Text.Json.JsonSerializer.Serialize(Songs, Utils.Jso));

        // return;
        foreach (Song song in Songs)
        {
            int mId = await DbManager.InsertSong(song);
            // Console.WriteLine($"Inserted mId {mId}");
        }
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static async Task<List<Song>> ImportMusicBrainzDataInner(IEnumerable<MusicBrainzJson> json)
    {
        var songs = new List<Song>();

        var releaseGroups = json.OrderBy(x => x.release_group.id).GroupBy(y => y.release_group.id);
        foreach (IGrouping<int, MusicBrainzJson> releaseGroup in releaseGroups)
        {
            // pick the first release (by id) of the release group and only import songs from that release
            var selectedRelease = releaseGroup.GroupBy(x => x.release.id).OrderBy(y => y.Key).First();
            var selectedReleaseValues = selectedRelease
                .Select(x => x)
                .OrderBy(y => y.medium.position)
                .ThenBy(y => y.track.position).ToList();

            string releaseGid = selectedReleaseValues.First().release.gid.ToString();
            if (Blacklists.MusicBrainzImporterReleaseBlacklist.Contains(releaseGid))
            {
                string releaseName = selectedReleaseValues.First().release.name;
                Console.WriteLine($"skipping blacklisted release {releaseGid} {releaseName}");
                continue;
            }

            for (int index = 0; index < selectedReleaseValues.Count(); index++)
            {
                MusicBrainzJson data = selectedReleaseValues.ElementAt(index);
                // Console.WriteLine($"{index} {data.release.gid}");

                // if (data.release.gid.ToString() == "19e9e324-e700-4c96-9a6c-cf958fb061e2")
                // {
                //     Console.WriteLine();
                // }

                // characters linked to their cv. instead of their own pages break stuff
                // e.g.: https://musicbrainz.org/recording/6b877bfb-916e-4b8c-80ca-926c4b11eda0
                data.artist = data.artist.DistinctBy(x => x.id).ToArray();

                // todo cv.
                var songArtists = new List<SongArtist>();
                foreach (artist artist in data.artist)
                {
                    string vndbid = artist.gid.ToString();
                    if (MusicBrainzVndbArtistDict.TryGetValue(artist.gid, out var o))
                    {
                        vndbid = o.First(); // todo

                        if (o.Length > 1)
                        {
                            Console.WriteLine(
                                $"artist has more than one vndb page linked: https://musicbrainz.org/artist/{artist.gid} {artist.name} {artist.sort_name}");
                        }
                    }
                    else
                    {
                        // Console.WriteLine(
                        //     $"artist not linked: https://musicbrainz.org/artist/{artist.gid} {artist.name} {artist.sort_name}");
                    }

                    SongArtist songArtist = new SongArtist()
                    {
                        VndbId = vndbid,
                        // Role = role, // todo
                        PrimaryLanguage = artist.area.ToString(), // todo str
                        Titles =
                            new List<Title>()
                            {
                                new Title()
                                {
                                    LatinTitle = artist.sort_name,
                                    NonLatinTitle = artist.name,
                                    Language = artist.area.ToString() ?? "ja", // todo str
                                    IsMainTitle = false // todo?
                                },
                            },
                        Sex = (Sex)(artist.gender ?? 0) // todo str
                    };
                    songArtists.Add(songArtist);
                }

                // todo event_release? doesn't matter except for no vndb sources
                var airDateStart = DateTime.UtcNow;

                var musicSourceTitles = new List<Title>()
                {
                    new Title
                    {
                        // todo vndb vn title
                        LatinTitle = data.release.name,
                        NonLatinTitle = data.release.name,
                        Language = "ja", // data.release.language.ToString()!, // todo
                        IsMainTitle = true
                    }
                };

                if (!musicSourceTitles.Any(x => x.IsMainTitle))
                {
                    throw new Exception();
                }

                var song = new Song()
                {
                    MusicBrainzRecordingGid = data.recording.gid,
                    Type = SongType.Instrumental, // todo? only use instrumental for karaoke versions of vocal songs?
                    Titles =
                        new List<Title>()
                        {
                            new Title()
                            {
                                LatinTitle = data.track.name, // todo
                                NonLatinTitle = data.track.name, // todo
                                Language =
                                    "ja", // data.release.language.ToString()!, // TODO: release.language can be null
                                IsMainTitle = true
                            },
                        }.OrderBy(y => y.LatinTitle).ToList(),
                    Artists = songArtists.OrderBy(y => y.Titles.First().LatinTitle).ToList(),
                    Sources = new List<SongSource>()
                    {
                        new SongSource()
                        {
                            AirDateStart = airDateStart,
                            SongTypes = new List<SongSourceSongType>() { SongSourceSongType.BGM },
                            LanguageOriginal =
                                "ja", // data.release.language.ToString()!, // TODO: release.language is track language, not VN language
                            Type = SongSourceType.VN,
                            Links = new List<SongSourceLink>()
                            {
                                // new SongSourceLink()
                                // {
                                //     Type = SongSourceLinkType.MusicBrainzRelease,
                                //     Url = $"https://musicbrainz.org/release/{data.release.gid}"
                                // },
                                // todo fix InsertSong
                                new SongSourceLink()
                                {
                                    Type = SongSourceLinkType.VNDB, Url = $"https://vndb.org/{data.aaa_rids.vndbid}"
                                }
                            },
                            Titles = musicSourceTitles,
                        },
                    },
                };

                Song? sameSong = songs.FirstOrDefault(x =>
                    x.MusicBrainzRecordingGid!.Value == song.MusicBrainzRecordingGid!.Value);
                // if (sameSong is not null)
                // {
                //     Console.WriteLine(
                //         $"Same song! {song.Sources.First().Titles.First().LatinTitle} <-> {sameSong.Sources.First().Titles.First().LatinTitle}");
                // }
                // songs.Add(song);

                // todo
                if (sameSong is not null)
                {
                    Console.WriteLine(
                        $"Same song! {song.Sources.First().Titles.First().LatinTitle} <-> {sameSong.Sources.First().Titles.First().LatinTitle}");
                    sameSong.Sources.AddRange(song.Sources.Except(sameSong.Sources));
                }
                else
                {
                    songs.Add(song);
                }

                // todo
                // Guid releaseGid = new(song.Sources.Select(x =>
                //         x.Links.Single(y => y.Type == SongSourceLinkType.MusicBrainzRelease).Url).Single()
                //     .Replace("https://musicbrainz.org/release/", ""));
                // Guid recordingGid = song.MusicBrainzRecordingGid!.Value;

                var musicBrainzReleaseRecording = new MusicBrainzReleaseRecording
                {
                    release = data.release.gid, recording = data.recording.gid
                };
                await DbManager.InsertMusicBrainzReleaseRecording(musicBrainzReleaseRecording);
            }
        }

        return songs;
    }
}
