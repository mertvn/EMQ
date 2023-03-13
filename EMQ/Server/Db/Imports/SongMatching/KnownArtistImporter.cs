using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EMQ.Server.Db.Imports.SongMatching;

public static class KnownArtistImporter
{
    public static async Task ImportKnownArtist()
    {
        string dir = "M:\\!matching\\Suzuyu";
        // dir = "M:\\a";
        var regex = new Regex("", RegexOptions.Compiled);
        string extension = "*";
        var artistName = new List<string>() { "Suzuyu" };

        var songMatches = SongMatcher.ParseSongFile(dir, regex, extension, false);
        foreach (SongMatch songMatch in songMatches)
        {
            songMatch.Artists.AddRange(artistName);
        }

        await SongMatcher.Match(songMatches, "C:\\emq\\matching\\artist\\Suzuyu_1", false);
    }
}
