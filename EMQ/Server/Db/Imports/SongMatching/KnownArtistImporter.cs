using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Server.Db.Imports.SongMatching.Common;

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

    public static async Task ImportKnownArtistWithDir(string dir, int num)
    {
        string artistDirName = Path.GetFileName(dir);
        // dir = "M:\\a";
        var regex = new Regex("", RegexOptions.Compiled);
        string extension = "*";
        var artistName = new List<string>() { artistDirName };

        var songMatches = SongMatcher.ParseSongFile(dir, regex, extension, false);
        foreach (SongMatch songMatch in songMatches)
        {
            songMatch.Artists.Clear();
            songMatch.Artists.AddRange(artistName);
        }

        await SongMatcher.Match(songMatches, $"C:\\emq\\matching\\artist\\{artistDirName}_{num}", false);
    }
}
