using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Client;
using EMQ.Server;
using EMQ.Server.Controllers;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Imports.EGS;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.MusicBrainz.Business;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NUnit.Framework;

namespace Tests;

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

    // todo use xrefs in db instead of from file
    // todo make use of mel egs links
    // todo find more ways to match (manual stuff)
    [Test, Explicit]
    public async Task InsertCALFromEgs()
    {
        bool artistCheckMode = false;
        string date = Constants.ImportDateEgs;
        string folder = $"C:\\emq\\egs\\{date}";
        var json = JsonSerializer.Deserialize<EgsImporterInnerResult[]>(
            await File.ReadAllTextAsync($"{folder}\\matched.json"), Utils.JsoIndented)!;

        var egsDataCreaterDict = new Dictionary<string, EgsDataCreater>();
        string[] createrlistRows = await File.ReadAllLinesAsync($"{folder}\\createrlist.tsv");
        for (int i = 1; i < createrlistRows.Length; i++)
        {
            string createrlistRow = createrlistRows[i];
            string[] split = createrlistRow.Split("\t");
            var egsDataCreater = new EgsDataCreater { Id = split[0], Name = split[1], Furigana = split[2] };
            egsDataCreaterDict[egsDataCreater.Id] = egsDataCreater;
        }

        var xRefs = new List<XRef>();
        string[] xRefsRows = await File.ReadAllLinesAsync($"{folder}\\xrefs.csv");
        for (int i = 1; i < xRefsRows.Length; i++)
        {
            string xRefRow = xRefsRows[i];
            string[] split = xRefRow.Split(',');
            var xRef = new XRef { Vndb = split[0], Vgmdb = split[1], Anison = split[2], Egs = split[3] };
            xRefs.Add(xRef);
        }

        SongArtistRole[] songArtistRoles =
        {
            SongArtistRole.Composer, SongArtistRole.Arranger, SongArtistRole.Lyricist
        };

        // todo handle multiple vocalists thing
        var calDict = new ConcurrentDictionary<string, List<(int, int)>>();
        var xRefCacheDict = new ConcurrentDictionary<string, Dictionary<int, Dictionary<int, SongArtist>>>();
        await Parallel.ForEachAsync(json, new ParallelOptions() { MaxDegreeOfParallelism = 4 },
            async (egsImporterInnerResult, _) =>
            {
                await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
                foreach (SongArtistRole songArtistRole in songArtistRoles)
                {
                    int?[] arr = songArtistRole switch
                    {
                        SongArtistRole.Composer => egsImporterInnerResult.EgsData.Composer,
                        SongArtistRole.Arranger => egsImporterInnerResult.EgsData.Arranger,
                        SongArtistRole.Lyricist => egsImporterInnerResult.EgsData.Lyricist,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    foreach (int? id in arr)
                    {
                        if (id == null)
                        {
                            continue;
                        }

                        var egsDataCreater = egsDataCreaterDict[id.Value.ToString()];
                        if (!calDict.TryGetValue(egsDataCreater.Id, out var aIds))
                        {
                            // Console.WriteLine(egsDataCreater.Id);
                            var xRef = xRefs.Where(x => x.Egs == egsDataCreater.Id).ToArray();
                            var first = xRef.FirstOrDefault();
                            int distinctCount = xRef.DistinctBy(x => x.Vndb).Count();
                            if (!xRef.Any() || distinctCount != 1)
                            {
                                if (distinctCount > 0)
                                {
                                    Console.WriteLine($"xRef Count isn't 1: {egsDataCreater.Id}");
                                    continue;
                                }
                            }

                            if (!string.IsNullOrEmpty(first.Vndb))
                            {
                                bool cached = xRefCacheDict.TryGetValue(first.Vndb, out var artists);
                                if (!cached)
                                {
                                    artists = await DbManager.SelectArtistBatchNoAM(connection,
                                        new List<Song>()
                                        {
                                            new()
                                            {
                                                Artists = new List<SongArtist>
                                                {
                                                    new()
                                                    {
                                                        Links = new List<SongArtistLink>
                                                        {
                                                            // todo? search with other xRef as well
                                                            new()
                                                            {
                                                                Url = first.Vndb.ToVndbUrl(),
                                                                Type = SongArtistLinkType.VNDBStaff,
                                                            },
                                                        }
                                                    }
                                                }
                                            }
                                        }, true);
                                    xRefCacheDict[first.Vndb] = artists;
                                }

                                if (artists!.Any())
                                {
                                    // todo try to find actual credited name on egs
                                    var single = artists!.Single().Value.Single();
                                    aIds = new List<(int, int)>
                                    {
                                        (single.Key,
                                            (single.Value.Titles.FirstOrDefault(x => x.IsMainTitle) ??
                                             single.Value.Titles.First()).ArtistAliasId)
                                    };
                                }
                                else if (!cached)
                                {
                                    Console.WriteLine($"xRef doesn't exist in EMQ db: {first.Vndb}");
                                    continue;
                                }
                            }
                            else
                            {
                                string name = egsDataCreater.Name;
                                int firstParenthesisIndex = name.IndexOf('(');
                                if (firstParenthesisIndex > 0)
                                {
                                    name = name[..firstParenthesisIndex];
                                }

                                if (!Setup.BlacklistedCreaterNames.Any(x => x == name.NormalizeForAutocomplete()))
                                {
                                    aIds = await DbManager.FindArtistIdsByArtistNames(new List<string> { name });
                                }
                            }

                            if (aIds != null && aIds.Any())
                            {
                                calDict[egsDataCreater.Id] = aIds;
                            }
                        }
                    }
                }
            });

        Console.WriteLine($"{calDict.Count(x => x.Value.Count == 1)}/{calDict.Count}");
        foreach ((string? key, _) in calDict.Where(x => x.Value.Count > 1))
        {
            Console.WriteLine(
                $"aIds.Count > 1: https://erogamescape.dyndns.org/~ap2/ero/toukei_kaiseki/creater.php?creater={key}");
        }

        // return;
        json = json.DistinctBy(x => x.mIds.Single()).ToArray();
        var artistAliasDict = new Dictionary<int, SongArtist>();
        var songsDict =
            (await DbManager.SelectSongsMIds(json.SelectMany(x => x.mIds).ToArray(), false))
            .ToDictionary(x => x.Id, x => x);

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        foreach (EgsImporterInnerResult egsImporterInnerResult in json)
        {
            bool addedSomething = false;
            int mId = egsImporterInnerResult.mIds.Single();
            var song = songsDict[mId];
            // foreach (SongArtist songArtist in song.Artists)
            // {
            //     if (songArtist.VndbId == "s2384")
            //     {
            //     }
            // }

            // if (egsImporterInnerResult.EgsData.MusicId != 2887)
            // {
            //     continue;
            // }

            // if (song.Id != 1049)
            // {
            //     continue;
            // }

            foreach (SongArtistRole songArtistRole in songArtistRoles)
            {
                int?[] arr = songArtistRole switch
                {
                    SongArtistRole.Composer => egsImporterInnerResult.EgsData.Composer,
                    SongArtistRole.Arranger => egsImporterInnerResult.EgsData.Arranger,
                    SongArtistRole.Lyricist => egsImporterInnerResult.EgsData.Lyricist,
                    _ => throw new ArgumentOutOfRangeException()
                };

                foreach (int? id in arr)
                {
                    if (id == null)
                    {
                        continue;
                    }

                    if (calDict.TryGetValue(id.Value.ToString(), out List<(int, int)>? aIds) && aIds.Count == 1)
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
                                        new Song
                                        {
                                            Artists = new List<SongArtist>()
                                            {
                                                new()
                                                {
                                                    Id = aId,
                                                    Titles = new List<Title>()
                                                    {
                                                        new() { ArtistAliasId = aaId }
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

                        if (!artist!.Roles.Contains(songArtistRole))
                        {
                            artist.Roles.Add(songArtistRole);
                            addedSomething = true;
                        }
                    }
                }
            }

            if (addedSomething)
            {
                if (song.Artists.Any(x => x.Titles.Count != 1))
                {
                    throw new Exception($"artists must have exactly one title per song: {song}");
                }

                if (!song.Artists.Any(x => x.Roles.Contains(SongArtistRole.Vocals)))
                {
                    throw new Exception($"At least one artist must have the Vocals role.");
                }

                string? existingUrl = song.Links.SingleOrDefault(x => x.Type == SongLinkType.ErogameScapeMusic)?.Url;
                string noteUser =
                    $"https://erogamescape.dyndns.org/~ap2/ero/toukei_kaiseki/music.php?music={egsImporterInnerResult.EgsData.MusicId}";

                if (existingUrl != null && existingUrl != noteUser)
                {
                    Console.WriteLine($"skipping mismatched urls {existingUrl} vs {noteUser}");
                    continue;
                }

                // Console.WriteLine(song);
                var actionResult = await ServerUtils.BotEditSong(new ReqEditSong(song, false, noteUser));
                if (actionResult is not OkResult)
                {
                    var badRequestObjectResult = actionResult as BadRequestObjectResult;
                    Console.WriteLine($"actionResult is not OkResult: {song} {badRequestObjectResult?.Value}");
                }
            }

            foreach (SongArtist songArtist in song.Artists)
            {
                songArtist.Roles =
                    songArtist.Roles.Where(x => x is SongArtistRole.Unknown or SongArtistRole.Vocals).ToList();
            }
        }
    }

    [Test, Explicit]
    public async Task InsertCALFromCreditsCsv()
    {
        string date = Constants.ImportDateEgs;
        string folder = $"C:\\emq\\egs\\{date}";

        var credits = new List<Credit>();
        string[] creditsRows = await File.ReadAllLinesAsync($"{folder}\\credits.csv");
        for (int i = 1; i < creditsRows.Length; i++)
        {
            string creditRow = creditsRows[i];
            string[] split = creditRow.Split(',');
            var credit = new Credit
            {
                MusicId = Convert.ToInt32(split[0]),
                VndbId = split[1],
                Type = Enum.Parse<SongArtistRole>(split[2].Split('#')[0]),
            };
            credits.Add(credit);
        }

        var controller = new LibraryController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["EMQ_SESSION"] =
            new Session(new Player(1, "Cookie4IS", Avatar.DefaultAvatar), "", UserRoleKind.User, null);

        var songsDict =
            (await DbManager.SelectSongsMIds(credits.Select(x => x.MusicId).ToArray(), false))
            .ToDictionary(x => x.Id, x => x);

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        foreach (IGrouping<int, Credit> grouping in credits.GroupBy(x => x.MusicId))
        {
            var song = songsDict[grouping.First().MusicId].Clone();
            bool addedSomething = true;
            foreach (Credit credit in grouping)
            {
                // Console.WriteLine(credit);
                var songArtistRole = credit.Type;
                string vndbUrl = credit.VndbId.ToVndbUrl();

                // check if song already has that artist and ignore alias if so
                var artist = song.Artists.SingleOrDefault(x =>
                    x.Links.Any(y => y.Type == SongArtistLinkType.VNDBStaff && y.Url == vndbUrl));
                bool artistWasAlreadyAdded = artist != null;
                if (!artistWasAlreadyAdded)
                {
                    // if (!artistAliasDict.TryGetValue(aaId, out artist))
                    // {
                    try
                    {
                        artist = (await DbManager.SelectArtistBatchNoAM(connection,
                            new List<Song>
                            {
                                new Song
                                {
                                    Artists = new List<SongArtist>
                                    {
                                        new()
                                        {
                                            Links = new List<SongArtistLink> { new() { Url = vndbUrl } }
                                        },
                                    }
                                }
                            }, false)).Single().Value.Single().Value;
                        // artistAliasDict[aaId] = artist;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"credit not in emq: {credit.VndbId}");
                        continue;
                    }
                    // }
                }

                if (!artistWasAlreadyAdded)
                {
                    song.Artists.Add(artist!);
                }

                if (!artist!.Roles.Contains(songArtistRole))
                {
                    artist.Roles.Add(songArtistRole);
                    addedSomething = true;
                }
            }

            if (addedSomething)
            {
                foreach (SongArtist songArtist in song.Artists)
                {
                    songArtist.Titles = new List<Title>
                    {
                        songArtist.Titles.SingleOrDefault(x => x.IsMainTitle) ?? songArtist.Titles.First()
                    };
                }

                var actionResult = await controller.EditSong(new ReqEditSong(song, false, "from credits.csv"));
                if (actionResult is not OkResult)
                {
                    var badRequestObjectResult = actionResult as BadRequestObjectResult;
                    Console.WriteLine($"actionResult is not OkResult: {song} {badRequestObjectResult?.Value}");
                }
            }
        }
    }

    [Test, Explicit]
    public async Task ImportXrefs()
    {
        string date = Constants.ImportDateEgs;
        string folder = $"C:\\emq\\egs\\{date}";
        var xRefs = new List<XRef>();
        string[] xRefsRows = await File.ReadAllLinesAsync($"{folder}\\xrefs.csv");
        for (int i = 1; i < xRefsRows.Length; i++)
        {
            string xRefRow = xRefsRows[i];
            string[] split = xRefRow.Split(',');
            xRefs.Add(new XRef { Vndb = split[0], Vgmdb = split[1], Anison = split[2], Egs = split[3] });
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionVndb = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb());
        var controller = new LibraryController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["EMQ_SESSION"] =
            new Session(new Player(1, "Cookie4IS", Avatar.DefaultAvatar), "", UserRoleKind.User, null);

        foreach (XRef xRef in xRefs)
        {
            var mbArtistIdFromVndbDB =
                await connectionVndb.QuerySingleOrDefaultAsync<Guid?>("select l_mbrainz from staff where id = @id",
                    new { id = xRef.Vndb });
            // if (mbArtistIdFromVndbDB is null)
            // {
            //     continue;
            // }

            var artists = await DbManager.SelectArtistBatchNoAM(connection,
                new List<Song>
                {
                    new()
                    {
                        Artists = new List<SongArtist>
                        {
                            new()
                            {
                                Links = new List<SongArtistLink>
                                {
                                    new() { Url = xRef.Vndb.ToVndbUrl() },
                                    new() { Url = xRef.Vgmdb.ToVndbUrl() },
                                    new() { Url = xRef.Anison.ToVndbUrl() },
                                    new() { Url = xRef.Egs.ToVndbUrl() },
                                    new() { Url = $"https://musicbrainz.org/artist/{mbArtistIdFromVndbDB}" },
                                }
                            }
                        }
                    }
                }, true);
            if (!artists.Any())
            {
                Console.WriteLine($"needs inserting: {xRef.Vndb}");
                continue;
            }

            var single = artists.Single();
            if (single.Value.Count > 1)
            {
                Console.WriteLine($"needs merging: {xRef.Vndb}");
                continue;
            }

            bool addedSomething = false;
            var artist = single.Value.Single().Value;
            if (!string.IsNullOrEmpty(xRef.Vndb) &&
                !artist.Links.Any(x => x.Type == SongArtistLinkType.VNDBStaff))
            {
                addedSomething = true;
                artist.Links.Add(new SongArtistLink
                {
                    Url = $"https://vndb.org/{xRef.Vndb}", Type = SongArtistLinkType.VNDBStaff, Name = "",
                });
            }

            if (!string.IsNullOrEmpty(xRef.Vgmdb) &&
                !artist.Links.Any(x => x.Type == SongArtistLinkType.VGMdbArtist))
            {
                addedSomething = true;
                artist.Links.Add(new SongArtistLink
                {
                    Url = $"https://vgmdb.net/artist/{xRef.Vgmdb}",
                    Type = SongArtistLinkType.VGMdbArtist,
                    Name = "",
                });
            }

            if (!string.IsNullOrEmpty(xRef.Anison) &&
                !artist.Links.Any(x => x.Type == SongArtistLinkType.AnisonInfoPerson))
            {
                addedSomething = true;
                artist.Links.Add(new SongArtistLink
                {
                    Url = $"http://anison.info/data/person/{xRef.Anison}.html",
                    Type = SongArtistLinkType.AnisonInfoPerson,
                    Name = "",
                });
            }

            if (!string.IsNullOrEmpty(xRef.Egs) &&
                !artist.Links.Any(x => x.Type == SongArtistLinkType.ErogameScapeCreater))
            {
                addedSomething = true;
                artist.Links.Add(new SongArtistLink
                {
                    Url =
                        $"https://erogamescape.dyndns.org/~ap2/ero/toukei_kaiseki/creater.php?creater={xRef.Egs}",
                    Type = SongArtistLinkType.ErogameScapeCreater,
                    Name = "",
                });
            }

            if (mbArtistIdFromVndbDB != null &&
                !artist.Links.Any(x => x.Type == SongArtistLinkType.MusicBrainzArtist))
            {
                addedSomething = true;
                artist.Links.Add(new SongArtistLink
                {
                    Url = $"https://musicbrainz.org/artist/{mbArtistIdFromVndbDB}",
                    Type = SongArtistLinkType.MusicBrainzArtist,
                    Name = "",
                });
            }

            // if (artist.Titles.Count == 1 && !artist.Titles.First().IsMainTitle)
            // {
            //     artist.Titles.First().IsMainTitle = true;
            // }

            if (addedSomething)
            {
                var actionResult =
                    await controller.EditArtist(new ReqEditArtist(artist, false,
                        $"from VNDB (MB) and xrefs.csv: {xRef.ToString()}"));
                if (actionResult is not OkResult)
                {
                    var badRequestObjectResult = actionResult as BadRequestObjectResult;
                    Console.WriteLine($"actionResult is not OkResult: {artist} {badRequestObjectResult?.Value}");
                }
            }
        }
    }

    // todo use this to sync aliases for existing artists as well?
    [Test, Explicit]
    public async Task ImportMissingVndbStaff()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await using var connectionVndb = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Vndb());
        var controller = new LibraryController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Items["EMQ_SESSION"] =
            new Session(new Player(1, "Cookie4IS", Avatar.DefaultAvatar), "", UserRoleKind.User, null);

        string[] emqArtistVndbIds =
            (await connection.QueryAsync<string>(
                $"select url from artist_external_link where type = {(int)SongArtistLinkType.VNDBStaff}"))
            .Select(x => x.ToVndbId()).ToArray();

        var artistsJson = (await connectionVndb.QueryAsync<dynamic>(@"
SELECT s.*
FROM vn_staff vs
JOIN vn v ON v.id = vs.id
JOIN releases_vn rv ON rv.vid = v.id
JOIN releases r ON r.id = rv.id
JOIN staff_alias sa ON sa.aid = vs.aid
JOIN staff s ON s.id = sa.id
WHERE lang = 'ja' AND (vs.role in ('music') OR vs.note ~* 'lyric') and not s.id = any(@emqArtistVndbIds)
GROUP BY s.id", new { emqArtistVndbIds })).ToList();

        var artists_aliasesLookup = (await connectionVndb.QueryAsync<dynamic>(@"
SELECT sa.id, sa.aid, name, latin
FROM staff s
JOIN staff_alias sa ON sa.id = s.id
WHERE s.lang = 'ja'"))
            .ToLookup(x => (string)x.id); // indexed by staff id as opposed to alias id, unlike VndbImporter!

        var songArtists = new List<SongArtist>();
        foreach (dynamic dynArtist in artistsJson)
        {
            var titles = new List<Title>();
            foreach (dynamic dynArtistAlias in artists_aliasesLookup[dynArtist.id])
            {
                (string artistLatinTitle, string? artistNonLatinTitle) = Utils.VndbTitleToEmqTitle(
                    (string)dynArtistAlias.name,
                    (string?)dynArtistAlias.latin);
                titles.Add(new Title
                {
                    LatinTitle = artistLatinTitle,
                    NonLatinTitle = artistNonLatinTitle,
                    Language = dynArtist.lang,
                    IsMainTitle = (int)dynArtist.main == (int)dynArtistAlias.aid
                });
            }

            SongArtist songArtist = new()
            {
                Roles = new List<SongArtistRole>(), PrimaryLanguage = dynArtist.lang, Titles = titles,
            };
            songArtists.Add(songArtist);

            var l_vndb = (string?)dynArtist.id;
            var l_vgmdb = (int?)dynArtist.l_vgmdb;
            // var l_anison = dynArtist.l_anison;
            // svar l_egs = dynArtist.l_egs;
            var l_mbrainz = (Guid?)dynArtist.l_mbrainz;

            if (!string.IsNullOrEmpty(l_vndb))
            {
                songArtist.Links.Add(new SongArtistLink
                {
                    Url = $"https://vndb.org/{l_vndb}", Type = SongArtistLinkType.VNDBStaff, Name = "",
                });
            }

            if (l_vgmdb is > 0)
            {
                songArtist.Links.Add(new SongArtistLink
                {
                    Url = $"https://vgmdb.net/artist/{l_vgmdb}", Type = SongArtistLinkType.VGMdbArtist, Name = "",
                });
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (l_mbrainz is not null && l_mbrainz != Guid.Empty)
            {
                songArtist.Links.Add(new SongArtistLink
                {
                    Url = $"https://musicbrainz.org/artist/{l_mbrainz}",
                    Type = SongArtistLinkType.MusicBrainzArtist,
                    Name = "",
                });
            }
        }

        foreach (SongArtist songArtist in songArtists)
        {
            // Console.WriteLine(songArtist.ToString());
            var actionResult =
                await controller.EditArtist(new ReqEditArtist(songArtist, true,
                    "new composers and lyricists from VNDB"));
            if (actionResult is not OkResult)
            {
                var badRequestObjectResult = actionResult as BadRequestObjectResult;
                Console.WriteLine($"actionResult is not OkResult: {songArtist} {badRequestObjectResult?.Value}");
            }
        }
    }

    [Test, Explicit]
    public async Task InsertArtistLinksFromVndb()
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        string date = "2025-04-20";
        string basePath = @$"C:\emq\vndbdumps\{date}\vndb-db-{date}\db";
        Dictionary<string, string[]> extlinks = (await File.ReadAllLinesAsync($"{basePath}/extlinks"))
            .Select(x => x.Split("\t"))
            .ToDictionary(x => x[0], x => x);

        var staffExtlinks = (await File.ReadAllLinesAsync($"{basePath}/staff_extlinks"))
            .Select(x => x.Split("\t"))
            .ToLookup(x => x[1], x => x);

        var wikidata = (await File.ReadAllLinesAsync($"{basePath}/wikidata"))
            .Select(x => x.Split("\t"))
            .ToDictionary(x => x[0], x => x);

        var aids = (await connection.GetListAsync<Artist>()).ToArray();
        var artists = ((await DbManager.SelectArtistBatchNoAM(connection,
            aids.Select(x => new Song() { Artists = new List<SongArtist>() { new() { Id = x.id } } }).ToList(),
            false)).SelectMany(x => x.Value.Select(y => y.Value))).ToArray();

        Dictionary<int, SongArtist> d = new();
        foreach ((string key, string[] value) in extlinks)
        {
            string type = value[1];
            IEnumerable<string[]> match = staffExtlinks[key];

            if (type is not ("mbrainz" or "vgmdb" or "egs_creator" or "anison" or "wikidata" or "anidb"))
            {
                continue;
            }

            string[]? first = match.FirstOrDefault();
            if (!(first?.Any() ?? false))
            {
                continue;
            }

            string vndbUrl = first[0].ToVndbUrl();
            var artist = artists.FirstOrDefault(x => x.Links.Any(y => y.Url == vndbUrl));
            if (artist == null)
            {
                // Console.WriteLine($"not on emq: {vndbUrl}");
                continue;
            }

            // todo
            // else if (artist.Links.Any(x => x.Url == url))
            // {
            //     if (!artist.Links.Any(x => x.Type == SongArtistLinkType.VNDBStaff))
            //     {
            //         Console.WriteLine($"can add {vndbUrl} to {url} for {artist}");
            //
            //     }
            // }

            // todo change this to check for url instead of type after the first batch is approved
            switch (type)
            {
                case "mbrainz":
                    {
                        string url = $"https://musicbrainz.org/artist/{value[2]}";
                        if (!artist.Links.Any(x => x.Type == SongArtistLinkType.MusicBrainzArtist))
                        {
                            // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                            artist.Links.Add(new SongArtistLink
                            {
                                Url = url, Type = SongArtistLinkType.MusicBrainzArtist,
                            });

                            if (!d.ContainsKey(artist.Id))
                            {
                                d[artist.Id] = artist;
                            }
                        }

                        break;
                    }
                case "vgmdb":
                    {
                        string url = $"https://vgmdb.net/artist/{value[2]}";
                        if (!artist.Links.Any(x => x.Type == SongArtistLinkType.VGMdbArtist))
                        {
                            // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                            artist.Links.Add(new SongArtistLink { Url = url, Type = SongArtistLinkType.VGMdbArtist, });

                            if (!d.ContainsKey(artist.Id))
                            {
                                d[artist.Id] = artist;
                            }
                        }

                        break;
                    }
                case "egs_creator":
                    {
                        string url =
                            $"https://erogamescape.dyndns.org/~ap2/ero/toukei_kaiseki/creater.php?creater={value[2]}";
                        if (!artist.Links.Any(x => x.Type == SongArtistLinkType.ErogameScapeCreater))
                        {
                            // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                            artist.Links.Add(new SongArtistLink
                            {
                                Url = url, Type = SongArtistLinkType.ErogameScapeCreater,
                            });

                            if (!d.ContainsKey(artist.Id))
                            {
                                d[artist.Id] = artist;
                            }
                        }

                        break;
                    }
                case "anison":
                    {
                        string url = $"http://anison.info/data/person/{value[2]}.html";
                        if (!artist.Links.Any(x => x.Type == SongArtistLinkType.AnisonInfoPerson))
                        {
                            // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                            artist.Links.Add(new SongArtistLink
                            {
                                Url = url, Type = SongArtistLinkType.AnisonInfoPerson,
                            });

                            if (!d.ContainsKey(artist.Id))
                            {
                                d[artist.Id] = artist;
                            }
                        }

                        break;
                    }
                case "wikidata":
                    {
                        string url = $"https://wikidata.org/wiki/Q{value[2]}";
                        if (!artist.Links.Any(x => x.Type == SongArtistLinkType.WikidataItem))
                        {
                            // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                            artist.Links.Add(new SongArtistLink { Url = url, Type = SongArtistLinkType.WikidataItem, });

                            if (!d.ContainsKey(artist.Id))
                            {
                                d[artist.Id] = artist;
                            }
                        }

                        break;
                    }
                case "anidb":
                    {
                        string url = $"https://anidb.net/creator/{value[2]}";
                        if (!artist.Links.Any(x => x.Type == SongArtistLinkType.AniDBCreator))
                        {
                            // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                            artist.Links.Add(new SongArtistLink { Url = url, Type = SongArtistLinkType.AniDBCreator, });

                            if (!d.ContainsKey(artist.Id))
                            {
                                d[artist.Id] = artist;
                            }
                        }

                        break;
                    }
            }
        }

        foreach (SongArtist artist in artists.Where(x => x.Links.Any(y => y.Type == SongArtistLinkType.WikidataItem)))
        {
            string wikidataId = artist.Links.Single(x => x.Type == SongArtistLinkType.WikidataItem).Url
                .Replace("https://", "").Replace("www.", "").Replace("wikidata.org/wiki/Q", "");
            if (wikidata.TryGetValue(wikidataId, out string[]? wikidataRow))
            {
                string mbId = wikidataRow[13];
                if (mbId != "\\N")
                {
                    string mbIdReplaced = mbId.Replace("{", "").Replace("}", "");
                    if (!Guid.TryParse(mbIdReplaced, out _))
                    {
                        Console.WriteLine($"invalid mbid: {mbIdReplaced}");
                        continue;
                    }

                    string url = $"https://musicbrainz.org/artist/{mbIdReplaced}";
                    if (!artist.Links.Any(x => x.Type == SongArtistLinkType.MusicBrainzArtist))
                    {
                        // Console.WriteLine($"can add {url} to {vndbUrl} for {artist}");
                        artist.Links.Add(new SongArtistLink
                        {
                            Url = url, Type = SongArtistLinkType.MusicBrainzArtist,
                        });

                        // let these get in the second run w/e
                        if (!d.ContainsKey(artist.Id))
                        {
                            d[artist.Id] = artist;
                        }
                    }
                }
            }
        }

        foreach ((int _, SongArtist? artist) in d)
        {
            var actionResult =
                await ServerUtils.BotEditArtist(new ReqEditArtist(artist, false,
                    "Links from VNDB"));
            if (actionResult is not OkResult)
            {
                var badRequestObjectResult = actionResult as BadRequestObjectResult;
                Console.WriteLine(
                    $"actionResult is not OkResult: {artist} {badRequestObjectResult?.Value}");
                if (badRequestObjectResult?.Value != null)
                {
                    if (int.TryParse(badRequestObjectResult.Value.ToString()!.Replace(
                            "An artist linked to at least one of the external links you've added already exists in the database: ea",
                            ""), out int id))
                    {
                        var actionResultMerge = await ServerUtils.BotEditMergeArtists(new MergeArtists
                        {
                            Id = id,
                            SourceId = artist.Id,
                            SourceName = Converters.GetSingleTitle(artist.Titles).LatinTitle,
                        });
                        if (actionResultMerge is not OkResult)
                        {
                            var badRequestObjectResultMerge = actionResultMerge as BadRequestObjectResult;
                            Console.WriteLine(
                                $"actionResult is not OkResult: {artist.Id} {id} {badRequestObjectResultMerge?.Value}");
                        }
                    }
                }
            }
        }

        // await transaction.CommitAsync();
    }

    [Test, Explicit]
    public async Task InsertCALFromMusicBrainz()
    {
        ClientState.MBArtistDict = await DbManager.GetMBArtists();
        var internalClient = new HttpClient { BaseAddress = new Uri(Constants.WebsiteDomain) };

        // todo extract this into a method
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        const string sqlMids = "SELECT msm.music_id, msm.type FROM music_source_music msm order by msm.music_id";
        Dictionary<int, HashSet<SongSourceSongType>> mids = (await connection.QueryAsync<(int, int)>(sqlMids))
            .GroupBy(x => x.Item1)
            .ToDictionary(y => y.Key, y => y.Select(z => (SongSourceSongType)z.Item2).ToHashSet());

        List<int> validMids = mids
            .Where(x => x.Value.Any(y => SongSourceSongTypeMode.Vocals.ToSongSourceSongTypes().Contains(y)))
            .Select(z => z.Key)
            .ToList();

        List<Song> songs =
            (await DbManager.SelectSongsMIds(validMids.ToArray(), false)).Where(x =>
                x.Links.Any(y => y.Type == SongLinkType.MusicBrainzRecording)).ToList();

        foreach (Song song in songs)
        {
            string recId = song.Links.Single(x => x.Type == SongLinkType.MusicBrainzRecording).Url
                .Replace("https://musicbrainz.org/recording/", "");
            var mbRecording = await MBApi.GetRecording(ServerUtils.Client, new Guid(recId));
            if (mbRecording == null)
            {
                return;
            }

            bool addedSomething = await MusicBrainzMethods.ProcessMBRelations(song, mbRecording.relations,
                internalClient, ClientState.MBArtistDict);

            if (addedSomething)
            {
                var actionResult = await ServerUtils.BotEditSong(new ReqEditSong(song, false, "C/A/L from MB"));
                if (actionResult is not OkResult)
                {
                    var badRequestObjectResult = actionResult as BadRequestObjectResult;
                    Console.WriteLine($"actionResult is not OkResult: {song} {badRequestObjectResult?.Value}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    [Test, Explicit]
    public async Task MatchAnison()
    {
        string filePath = @"N:\!anison\!anison_song.json";
        var json = JsonSerializer.Deserialize<Dictionary<int, AnisonSong>>(await File.ReadAllTextAsync(filePath),
            Utils.Jso)!;
        var songs = (await DbManager.GetAllSongs()).Where(x =>
            !x.Sources.Any(y => y.SongTypes.Contains(SongSourceSongType.BGM))).ToArray();

        // var songsTitles = songs.SelectMany(x =>
        //         x.Titles.Where(y => y.IsMainTitle && y.Language == "ja" && y.NonLatinTitle != null)
        //             .Select(y => (x.Id, y.NonLatinTitle!.NormalizeForAutocomplete())))
        //     .ToLookup(x => x.Item2, x => x.Id);
        //
        // var songsTitles2 = songs.SelectMany(x =>
        //         x.Titles.Where(y => y.IsMainTitle && y.Language == "ja")
        //             .Select(y => (x.Id, y.LatinTitle.NormalizeForAutocomplete())))
        //     .ToLookup(x => x.Item2, x => x.Id);
        //
        // var songsTitlesFinal = songsTitles.Concat(songsTitles2);

        var songsTitles = songs.SelectMany(x =>
                x.Titles.Where(y => y.IsMainTitle && y.Language == "ja")
                    .SelectMany(y => new[]
                    {
                        y.NonLatinTitle != null
                            ? (x.Id, y.NonLatinTitle.NormalizeForAutocomplete())
                            : (default, default),
                        (x.Id, y.LatinTitle.NormalizeForAutocomplete())
                    })
                    .Where(z => z.Item2 != default))
            .ToLookup(x => x.Item2, x => x.Id);

        SongArtistRole[] songArtistRoles =
        {
            SongArtistRole.Composer, SongArtistRole.Arranger, SongArtistRole.Lyricist
        };

        bool artistCheckMode = false;
        bool tryToMatchArtistsByNames = true;

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
            if (norm.Contains("Save the Soul".NormalizeForAutocomplete()))
            {
            }

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

                                if (addedSomething)
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
