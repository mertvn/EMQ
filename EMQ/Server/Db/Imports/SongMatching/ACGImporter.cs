using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Server.Db.Imports.SongMatching.Common;

namespace EMQ.Server.Db.Imports.SongMatching;

public static class ACGImporter
{
    public static async Task ImportACG()
    {
        string dir = "L:\\8b\\TOSORT\\[ACG音乐]日本动漫游戏主题曲大合集★☆★☆★【flac无损】";
        // dir = "M:\\a";
        var regex = new Regex("(.+) - (.+)().flac", RegexOptions.Compiled);
        string extension = "flac";

        var songMatches = SongMatcher.ParseSongFile(dir, regex, extension, true);
        await SongMatcher.Match(songMatches, "C:\\emq\\matching\\acg\\acg_2", false);
    }
}
