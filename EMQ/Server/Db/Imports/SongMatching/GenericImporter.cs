using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EMQ.Server.Db.Imports.SongMatching;

public static class GenericImporter
{
    public static async Task ImportGeneric()
    {
        string dir = "L:\\8b\\TOSORT\\agm";
        // dir = "M:\\a";
        var regex = new Regex("", RegexOptions.Compiled);
        string extension = "*";

        var songMatches = SongMatcher.ParseSongFile(dir, regex, extension, true);
        await SongMatcher.Match(songMatches, "C:\\emq\\matching\\generic\\gi_2-agm");
    }
}
