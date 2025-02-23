using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NUnit.Framework;

namespace Tests;

// todo move everything
public class EntryPoints_CAL
{
    public class AnisonSong
    {
        // public int Id { get; set; }

        public string Title { get; set; } = "";

        public Dictionary<int, AnisonSongArtist> Artists { get; set; } = new();

        // ReSharper disable once CollectionNeverQueried.Global
        public List<AnisonSongSource> Sources { get; set; } = new();

        public override string ToString() =>
            $"{Sources.FirstOrDefault()?.Title ?? "[no sources]"} {Title} by {Artists.FirstOrNull()?.Value.Title}";
    }

    public class AnisonSongArtist
    {
        // public int Id { get; set; }

        public string Title { get; set; } = "";

        // public string Character { get; set; } = "";

        // ReSharper disable once CollectionNeverQueried.Global
        public HashSet<string> Roles { get; set; } = new();
    }

    public class AnisonSongSource
    {
        public int Id { get; set; }

        public string Genre { get; set; } = "";

        public string Title { get; set; } = "";

        public string Type { get; set; } = "";
    }

    [Test, Explicit]
    public async Task MatchAnison()
    {
        string filePath = @"N:\!anison\!anison_song.json";
        var json = JsonSerializer.Deserialize<Dictionary<int, AnisonSong>>(await File.ReadAllTextAsync(filePath),
            Utils.Jso)!;
        var songs = (await DbManager.GetRandomSongs(int.MaxValue, true)).Where(x =>
            !x.Sources.Any(y => y.SongTypes.Contains(SongSourceSongType.BGM))).ToArray();
        var songsTitles = songs.SelectMany(x =>
                x.Titles.Where(y => y.IsMainTitle && y.NonLatinTitle != null)
                    .Select(y => (x.Id, y.NonLatinTitle!.NormalizeForAutocomplete())))
            .ToLookup(x => x.Item2, x => x.Id);

        SongArtistRole[] songArtistRoles =
        {
            SongArtistRole.Composer, SongArtistRole.Arranger, SongArtistRole.Lyricist
        };

        bool artistCheckMode = false;
        bool tryToMatchArtistsByNames = false;

        var calDict = new ConcurrentDictionary<string, List<(int, int)>>();
        var artistAliasDict = new Dictionary<int, SongArtist>();
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());

        var ael = (await connection.QueryAsync<(int, string)>(
                "SELECT artist_id, replace(replace(url, 'http://anison.info/data/person/', ''),'.html','') FROM artist_external_link ael WHERE type = 5"))
            .ToList();

        var artistsBatch = (await DbManager.SelectArtistBatchNoAM(connection,
            ael.Select(x => new Song() { Artists = new List<SongArtist>() { new() { Id = x.Item1 } } })
                .ToList(),
            false)).SelectMany(x => x.Value).ToList();

        foreach ((int aid, string anison) in ael)
        {
            var artist = artistsBatch.First(x => x.Key == aid).Value;
            artist.Roles.Clear();
            var title = artist.Titles.FirstOrDefault(x => x.IsMainTitle) ?? artist.Titles.First();
            calDict[anison] = new List<(int, int)> { (aid, title.ArtistAliasId) };
            artistAliasDict[title.ArtistAliasId] = artist;
        }

        // todo any instead of all?
        foreach ((int id, AnisonSong anison) in json.Where(x => x.Value.Sources.All(y => y.Genre.StartsWith("GM"))))
        {
            if (!anison.Sources.Any() || !anison.Artists.Any())
            {
                continue;
            }

            string norm = anison.Title.NormalizeForAutocomplete();
            if (songsTitles.Contains(norm))
            {
                // Console.WriteLine(anison.Title);
                int[] matches = songsTitles[norm].ToArray();
                switch (matches.Length)
                {
                    case 0:
                        // Console.WriteLine($"no matches");
                        break;
                    case 1:
                        int matchedId = matches[0];
                        var song = songs.First(x => x.Id == matchedId);
                        bool sourceMatches = song.Sources.Any(x =>
                            x.Titles.Any(
                                y => anison.Sources.Any(z =>
                                    z.Title.NormalizeForAutocomplete() ==
                                    (y.NonLatinTitle ?? "").NormalizeForAutocomplete() ||
                                    z.Title.NormalizeForAutocomplete() ==
                                    y.LatinTitle.NormalizeForAutocomplete())));
                        if (sourceMatches)
                        {
                            // todo alternatively check if any artist id the song already has matches
                            // todo use artist aliases
                            bool artistMatches = song.Artists.Any(x =>
                                x.Titles.Any(
                                    y => anison.Artists.Any(z =>
                                        z.Value.Roles.Contains("歌手") && // todo more checks
                                        z.Value.Title.NormalizeForAutocomplete() ==
                                        (y.NonLatinTitle ?? "").NormalizeForAutocomplete() ||
                                        z.Value.Title.NormalizeForAutocomplete() ==
                                        y.LatinTitle.NormalizeForAutocomplete())));
                            if (artistMatches)
                            {
                                Console.WriteLine($"{anison.Title} <=> {norm}");
                                bool hasAnisonLink = song.Links.Any(x => x.Type == SongLinkType.AnisonInfoSong);
                                if (hasAnisonLink) // todo?
                                {
                                    continue;
                                }

                                bool addedSomething = !hasAnisonLink; // todo?
                                addedSomething = false; // todo?

                                string url = $"http://anison.info/data/song/{id}.html";
                                song.Links.Add(new SongLink() { Type = SongLinkType.AnisonInfoSong, Url = url });

                                foreach ((int key, AnisonSongArtist? value) in anison.Artists)
                                {
                                    string anisonArtistIdStr = key.ToString();
                                    {
                                        if (!calDict.TryGetValue(anisonArtistIdStr, out var aIds) &&
                                            tryToMatchArtistsByNames)
                                        {
                                            string name = value.Title;
                                            // int firstParenthesisIndex = name.IndexOf('(');
                                            // if (firstParenthesisIndex > 0)
                                            // {
                                            //     name = name[..firstParenthesisIndex];
                                            // }

                                            if (!Setup.BlacklistedCreaterNames.Any(x =>
                                                    x == name.NormalizeForAutocomplete()))
                                            {
                                                aIds = await DbManager.FindArtistIdsByArtistNames(
                                                    new List<string> { name });
                                            }

                                            if (aIds != null && aIds.Any())
                                            {
                                                calDict[anisonArtistIdStr] = aIds;
                                            }
                                        }
                                    }

                                    {
                                        if (calDict.TryGetValue(anisonArtistIdStr, out List<(int, int)>? aIds) &&
                                            aIds.Count == 1)
                                        {
                                            (int aId, int aaId) = aIds.First();

                                            // check if song already has that artist and ignore alias if so
                                            var artist = song.Artists.SingleOrDefault(x => x.Id == aId);
                                            bool artistWasAlreadyAdded = artist != null;
                                            if (!artistWasAlreadyAdded)
                                            {
                                                if (!artistAliasDict.TryGetValue(aaId, out artist))
                                                {
                                                    artist = (await DbManager.SelectArtistBatchNoAM(connection,
                                                        new List<Song>()
                                                        {
                                                            new()
                                                            {
                                                                Artists = new List<SongArtist>()
                                                                {
                                                                    new()
                                                                    {
                                                                        Id = aId,
                                                                        Titles = new List<Title>()
                                                                        {
                                                                            new()
                                                                            {
                                                                                ArtistAliasId =
                                                                                    aaId
                                                                            }
                                                                        }
                                                                    },
                                                                }
                                                            }
                                                        }, false)).Single().Value.Single().Value;
                                                    artistAliasDict[aaId] = artist;
                                                }
                                                else if (artistCheckMode)
                                                {
                                                    continue;
                                                }

                                                artist.Roles.Clear();
                                            }

                                            if (!artistWasAlreadyAdded)
                                            {
                                                song.Artists.Add(artist!);
                                            }
                                            else
                                            {
                                                if (artistCheckMode)
                                                {
                                                    continue;
                                                }
                                            }

                                            foreach (SongArtistRole songArtistRole in songArtistRoles)
                                            {
                                                // todo more str and abstraction
                                                switch (songArtistRole)
                                                {
                                                    case SongArtistRole.Composer when !value.Roles.Contains("作曲"):
                                                    case SongArtistRole.Arranger when !value.Roles.Contains("編曲"):
                                                    case SongArtistRole.Lyricist when !value.Roles.Contains("作詞"):
                                                        continue;
                                                }

                                                if (!artist!.Roles.Contains(songArtistRole))
                                                {
                                                    artist.Roles.Add(songArtistRole);
                                                    addedSomething = true;
                                                }
                                            }
                                        }
                                    }
                                }

                                if (addedSomething)
                                {
                                    Console.WriteLine("addedSomething");
                                }

                                if (true && addedSomething) // todo
                                {
                                    var actionResult = await ServerUtils.BotEditSong(new ReqEditSong(song, false, url));
                                    if (actionResult is not OkResult)
                                    {
                                        var badRequestObjectResult = actionResult as BadRequestObjectResult;
                                        Console.WriteLine(
                                            $"actionResult is not OkResult: {song} {badRequestObjectResult?.Value}");
                                    }
                                }
                            }
                            else
                            {
                                // Console.WriteLine($"artist didn't match: {anison} vs {song} {song.Artists.FirstOrDefault()}");
                            }
                        }
                        else
                        {
                            // Console.WriteLine($"source didn't match: {anison} vs {song}");
                        }

                        break;
                    case > 1:
                        // Console.WriteLine($">1 matches");
                        break;
                }
            }
        }
    }
}
