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
        Dictionary<string, List<SongSourceSongType>> sourceVndbIds, List<string> artistVndbIds, int musicId,
        SongStats? songStats = null)
    {
        Titles = titles;
        Links = links;
        SourceVndbIds = sourceVndbIds;
        ArtistVndbIds = artistVndbIds;
        MusicId = musicId;
        SongStats = songStats;
    }

    public List<Title> Titles { get; set; }

    public List<SongLink> Links { get; set; }

    public Dictionary<string, List<SongSourceSongType>> SourceVndbIds { get; set; }

    public List<string> ArtistVndbIds { get; set; }

    public int MusicId { get; set; }

    public SongStats? SongStats { get; set; }

    public string EMQSongHash
    {
        get
        {
            if (Titles.Count != 1)
            {
                throw new Exception();
            }

            if (SourceVndbIds.Count < 1)
            {
                throw new Exception();
            }

            if (ArtistVndbIds.Count < 1)
            {
                throw new Exception();
            }

            foreach ((string key, List<SongSourceSongType> value) in SourceVndbIds)
            {
                SourceVndbIds[key] = value.Distinct().OrderBy(x => x).ToList();
            }

            string titles = Titles.Single().LatinTitle.ToLowerInvariant();
            string sources =
                JsonSerializer.Serialize(SourceVndbIds.OrderBy(x => Convert.ToInt32(x.Key.Replace("v", ""))));
            string artists = JsonSerializer.Serialize(ArtistVndbIds.OrderBy(x => x));

            string str = $"{titles};|;{sources};|;{artists}";
            return str;

            // byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
            // byte[] sha1 = SHA1.HashData(utf8Bytes);
            // return Convert.ToHexString(sha1);
        }
    }
}

// public class SongLiteSource
// {
//     public string VndbId { get; set; } = "";
//
//     public List<SongSourceSongType> Types { get; set; } = new();
// }
