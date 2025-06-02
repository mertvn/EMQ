using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.MusicBrainz.Business;

public class MusicBrainzMethods
{
    // todo? split into two (song, artist)
    // todo handle no-MBID-match case (requires some sort of queue for adding artists)
    public static async Task<bool> ProcessMBRelations(Song song, IEnumerable<MbRelation> relations,
        HttpClient client, Dictionary<string, int> mbArtistDict)
    {
        bool addedSomething = false;
        foreach (MbRelation mbRelation in relations)
        {
            switch (mbRelation.targettype)
            {
                case "artist":
                    {
                        switch (mbRelation.type)
                        {
                            case "vocal":
                                {
                                    if (mbArtistDict.TryGetValue(mbRelation.artist!.id, out int aid))
                                    {
                                        addedSomething |= await AddArtist(song, new AutocompleteA { AId = aid },
                                            new List<SongArtistRole> { SongArtistRole.Vocals }, client);
                                    }
                                    else
                                    {
                                        Console.WriteLine(
                                            $"unmatched mb artist: https://musicbrainz.org/artist/{mbRelation.artist!.id}");
                                    }

                                    break;
                                }
                            case "composer":
                                {
                                    if (mbArtistDict.TryGetValue(mbRelation.artist!.id, out int aid))
                                    {
                                        addedSomething |= await AddArtist(song, new AutocompleteA { AId = aid },
                                            new List<SongArtistRole> { SongArtistRole.Composer }, client);
                                    }
                                    else
                                    {
                                        Console.WriteLine(
                                            $"unmatched mb artist: https://musicbrainz.org/artist/{mbRelation.artist!.id}");
                                    }

                                    break;
                                }
                            case "arranger":
                                {
                                    if (mbArtistDict.TryGetValue(mbRelation.artist!.id, out int aid))
                                    {
                                        addedSomething |= await AddArtist(song, new AutocompleteA { AId = aid },
                                            new List<SongArtistRole> { SongArtistRole.Arranger }, client);
                                    }
                                    else
                                    {
                                        Console.WriteLine(
                                            $"unmatched mb artist: https://musicbrainz.org/artist/{mbRelation.artist!.id}");
                                    }

                                    break;
                                }
                            case "remixer":
                                {
                                    if (mbArtistDict.TryGetValue(mbRelation.artist!.id, out int aid))
                                    {
                                        addedSomething |= await AddArtist(song, new AutocompleteA { AId = aid },
                                            new List<SongArtistRole> { SongArtistRole.Arranger }, client);
                                    }
                                    else
                                    {
                                        Console.WriteLine(
                                            $"unmatched mb artist: https://musicbrainz.org/artist/{mbRelation.artist!.id}");
                                    }

                                    break;
                                }

                            case "lyricist":
                                {
                                    if (mbArtistDict.TryGetValue(mbRelation.artist!.id, out int aid))
                                    {
                                        addedSomething |= await AddArtist(song, new AutocompleteA { AId = aid },
                                            new List<SongArtistRole> { SongArtistRole.Lyricist }, client);
                                    }
                                    else
                                    {
                                        Console.WriteLine(
                                            $"unmatched mb artist: https://musicbrainz.org/artist/{mbRelation.artist!.id}");
                                    }

                                    break;
                                }
                        }
                    }
                    break;
                case "work":
                    switch (mbRelation.type)
                    {
                        // todo can works reference each other?
                        case "performance":
                            {
                                if (mbRelation.attributes.Contains("instrumental") ||
                                    mbRelation.attributes.Contains("karaoke"))
                                {
                                    song.Type |= SongType.Instrumental;
                                }

                                if (mbRelation.attributes.Contains("cover"))
                                {
                                    song.Type |= SongType.Cover;
                                }

                                if (song.Type > SongType.Standard)
                                {
                                    song.Type &= ~SongType.Standard;
                                }

                                addedSomething |=
                                    await ProcessMBRelations(song, mbRelation.work!.relations, client, mbArtistDict);
                                break;
                            }
                    }

                    break;
                case "url":
                    if (mbRelation.url!.resource.Contains("vndb.org"))
                    {
                        song.Artists.Single().Links.Add(new SongArtistLink
                        {
                            Url = mbRelation.url.resource, Type = SongArtistLinkType.VNDBStaff,
                        });
                    }
                    else if (mbRelation.url!.resource.Contains("vgmdb.net"))
                    {
                        song.Artists.Single().Links.Add(new SongArtistLink
                        {
                            Url = mbRelation.url.resource, Type = SongArtistLinkType.VGMdbArtist,
                        });
                    }
                    else if (mbRelation.url!.resource.Contains("anison.info"))
                    {
                        song.Artists.Single().Links.Add(new SongArtistLink
                        {
                            Url = mbRelation.url.resource, Type = SongArtistLinkType.AnisonInfoPerson,
                        });
                    }
                    else if (mbRelation.url!.resource.Contains("wikidata.org"))
                    {
                        song.Artists.Single().Links.Add(new SongArtistLink
                        {
                            Url = mbRelation.url.resource, Type = SongArtistLinkType.WikidataItem,
                        });
                    }
                    else if (mbRelation.url!.resource.Contains("anidb.net"))
                    {
                        song.Artists.Single().Links.Add(new SongArtistLink
                        {
                            Url = mbRelation.url.resource, Type = SongArtistLinkType.AniDBCreator,
                        });
                    }

                    break;
            }
        }

        return addedSomething;
    }

    // todo this isn't actually related to MB
    public static async Task<bool> AddArtist(Song song, AutocompleteA? selectedArtist, List<SongArtistRole>? roles,
        HttpClient client)
    {
        bool addedSomething = false;
        if (selectedArtist is null)
        {
            return addedSomething;
        }

        var existingArtist = song.Artists.FirstOrDefault(x => x.Id == selectedArtist.AId);
        if (existingArtist != null)
        {
            if (roles != null)
            {
                // todo idk if this is what we want in all cases
                foreach (SongArtistRole role in roles)
                {
                    if (!existingArtist.Roles.Contains(role))
                    {
                        existingArtist.Roles.Add(role);
                        addedSomething = true;
                    }
                }
            }

            return addedSomething;
        }

        var req = new SongArtist() { Id = selectedArtist.AId };
        HttpResponseMessage res1 = await client.PostAsJsonAsync("Library/GetSongArtist", req);
        if (res1.IsSuccessStatusCode)
        {
            var content = (await res1.Content.ReadFromJsonAsync<ResGetSongArtist>())!;

            // todo do this in a better way somehow
            SongArtist artist;
            if (content.SongArtists.Count == 1 && content.SongArtists.First().Titles.Count == 1)
            {
                artist = content.SongArtists.First();
            }
            else
            {
                if (string.IsNullOrEmpty(selectedArtist.AALatinAlias)) // from MBID match
                {
                    artist = content.SongArtists.First();
                    artist.Titles = new List<Title>
                    {
                        artist.Titles.FirstOrDefault(x => x.IsMainTitle) ?? artist.Titles.First()
                    };
                }
                else // normal
                {
                    // todo this fails when typing id:
                    artist = content.SongArtists.First(x => x.Titles.Any(y =>
                        y.LatinTitle == selectedArtist.AALatinAlias &&
                        ((y.NonLatinTitle == null && selectedArtist.AANonLatinAlias == "") ||
                         (y.NonLatinTitle == selectedArtist.AANonLatinAlias))));
                    artist.Titles = artist.Titles.Where(y =>
                        y.LatinTitle == selectedArtist.AALatinAlias &&
                        ((y.NonLatinTitle == null && selectedArtist.AANonLatinAlias == "") ||
                         (y.NonLatinTitle == selectedArtist.AANonLatinAlias))).ToList();
                }
            }

            artist.Roles = roles ?? artist.Roles;
            song.Artists.Add(artist);
            addedSomething = true;
        }

        return addedSomething;
    }
}
