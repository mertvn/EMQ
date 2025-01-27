using System;
using System.Collections.Generic;
using Dapper;
using EMQ.Shared.Quiz.Entities.Concrete;
using Npgsql;

namespace EMQ.Server;

public static class ArtistCollaborationFinder
{
    private class MusicCollaboration
    {
        public int MusicId { get; set; }
        public int[] ArtistGroup { get; set; } = Array.Empty<int>();
    }

    private static readonly Dictionary<(int, int), HashSet<int>> s_pairToMusicMap = new();

    static ArtistCollaborationFinder()
    {
        var allCollaborations =
            new NpgsqlConnection(ConnectionHelper.GetConnectionString()).Query<MusicCollaboration>($@"
            SELECT
                music_id as MusicId,
                array_agg(DISTINCT artist_id ORDER BY artist_id) as ArtistGroup
            FROM artist_music
            WHERE role != {(int)SongArtistRole.Lyricist}
            GROUP BY music_id");

        // Build index of artist pairs to music_ids
        foreach (var collab in allCollaborations)
        {
            // Create all possible pairs from the artist group
            for (int i = 0; i < collab.ArtistGroup.Length; i++)
            {
                for (int j = i + 1; j < collab.ArtistGroup.Length; j++)
                {
                    // Always use smaller ID first for consistent lookup
                    (int, int) pair = collab.ArtistGroup[i] <= collab.ArtistGroup[j]
                        ? (collab.ArtistGroup[i], collab.ArtistGroup[j])
                        : (collab.ArtistGroup[j], collab.ArtistGroup[i]);

                    if (!s_pairToMusicMap.TryGetValue(pair, out var musicSet))
                    {
                        musicSet = new HashSet<int>();
                        s_pairToMusicMap[pair] = musicSet;
                    }

                    musicSet.Add(collab.MusicId);
                }
            }
        }
    }

    public static HashSet<int> FindPairwiseCollaborations(int[] artistIds)
    {
        if (artistIds.Length < 2)
            return new HashSet<int>();

        var result = new HashSet<int>();

        // Check all possible pairs from input artists
        for (int i = 0; i < artistIds.Length; i++)
        {
            for (int j = i + 1; j < artistIds.Length; j++)
            {
                (int, int) pair = artistIds[i] <= artistIds[j]
                    ? (artistIds[i], artistIds[j])
                    : (artistIds[j], artistIds[i]);

                if (s_pairToMusicMap.TryGetValue(pair, out var musicSet))
                {
                    result.UnionWith(musicSet);
                }
            }
        }

        return result;
    }
}

// for finding exact artists match instead of any pair
// public class MusicCollaboration
// {
//     public int MusicId { get; set; }
//     public int[] ArtistGroup { get; set; } = Array.Empty<int>();
// }
//
// public class FastCollaborationFinder
// {
//     private readonly Dictionary<int, HashSet<int>> _artistToMusicMap;
//
//     public FastCollaborationFinder(IEnumerable<MusicCollaboration> allCollaborations)
//     {
//         _artistToMusicMap = new Dictionary<int, HashSet<int>>();
//
//         // Build inverse index: artist -> set of music_ids
//         foreach (var collab in allCollaborations)
//         {
//             foreach (var artistId in collab.ArtistGroup)
//             {
//                 if (!_artistToMusicMap.TryGetValue(artistId, out var musicSet))
//                 {
//                     musicSet = new HashSet<int>();
//                     _artistToMusicMap[artistId] = musicSet;
//                 }
//                 musicSet.Add(collab.MusicId);
//             }
//         }
//     }
//
//     public HashSet<int> FindPartialCollaborations(int[] artistIds)
//     {
//         if (artistIds.Length == 0)
//             return new HashSet<int>();
//
//         // Get music set for first artist
//         if (!_artistToMusicMap.TryGetValue(artistIds[0], out var result))
//             return new HashSet<int>();
//
//         // Intersect with music sets of other artists
//         for (int i = 1; i < artistIds.Length; i++)
//         {
//             if (!_artistToMusicMap.TryGetValue(artistIds[i], out var musicSet))
//                 return new HashSet<int>();
//
//             result.IntersectWith(musicSet);
//
//             if (result.Count == 0)
//                 break;
//         }
//
//         return result;
//     }
// }
//
// [Test, Explicit]
// public async Task f()
// {
//     using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
//     {
//         var allCollaborations = await connection.QueryAsync<MusicCollaboration>(@"
//     WITH grouped_music AS (
//         SELECT
//             music_id,
//             array_agg(DISTINCT artist_id ORDER BY artist_id) as artist_group
//         FROM artist_music
//         GROUP BY music_id
//     )
//     SELECT
//         music_id AS MusicId,
//         artist_group AS ArtistGroup
//     FROM grouped_music;");
//
//         var finder = new FastCollaborationFinder(allCollaborations);
//
//         // Find songs where these artists collaborated
//         int[] artistsToFind = new[] { 53, 409, 3450 };
//         var collaborations = finder.FindPartialCollaborations(artistsToFind);
//         foreach (int collaboration in collaborations)
//         {
//             Console.WriteLine(collaboration);
//         }
//     }
// }
