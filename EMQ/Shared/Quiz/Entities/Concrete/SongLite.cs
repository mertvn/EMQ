using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongLite
{
    public SongLite(List<Title> titles, List<SongLink> links,
        Dictionary<string, List<SongSourceSongType>> sourceVndbIds, List<SongArtist> artists, int musicId,
        SongStats? songStats = null)
    {
        Titles = titles;
        Links = links;
        SourceVndbIds = sourceVndbIds;
        Artists = artists;
        MusicId = musicId;
        SongStats = songStats;
    }

    public List<Title> Titles { get; set; }

    public List<SongLink> Links { get; set; }

    public Dictionary<string, List<SongSourceSongType>> SourceVndbIds { get; set; }

    public List<SongArtist> Artists { get; set; }

    public int MusicId { get; set; }

    public SongStats? SongStats { get; set; }

    public string EMQSongHash
    {
        get
        {
            if (SourceVndbIds.Count < 1)
            {
                throw new Exception();
            }

            if (Artists.Count < 1)
            {
                throw new Exception();
            }

            foreach ((string key, List<SongSourceSongType> value) in SourceVndbIds)
            {
                SourceVndbIds[key] = value.Distinct().OrderBy(x => x).ToList();
            }

            string titles = JsonSerializer.Serialize(Titles.OrderBy(x => x.LatinTitle)
                .Select(x => x.LatinTitle.ToLowerInvariant()));
            string sources = JsonSerializer.Serialize(SourceVndbIds.OrderBy(x => x.Key));
            string artists =
                JsonSerializer.Serialize(Artists.Where(x => x.Roles.Contains(SongArtistRole.Vocals))
                    .Select(artist => artist.VndbId ?? "").OrderBy(x => x).ToList());

            string str = $"{titles};|;{sources};|;{artists}";
            return str;
        }
    }
}

// public class SongLiteSource
// {
//     public string VndbId { get; set; } = "";
//
//     public List<SongSourceSongType> Types { get; set; } = new();
// }
