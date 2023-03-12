using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EMQ.Shared.Core;

namespace EMQ.Server.Db.Imports.SongMatching.GGVC;

public static class GGVCImporter
{
    public static async Task DeleteAlreadyImportedGGVCFiles()
    {
        var uploaded2 =
            JsonSerializer.Deserialize<List<Uploadable>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc2\\uploaded.json"),
                Utils.JsoIndented)!;

        var uploaded3 =
            JsonSerializer.Deserialize<List<Uploadable>>(
                await File.ReadAllTextAsync("C:\\emq\\ggvc3\\uploaded.json"),
                Utils.JsoIndented)!;

        // todo add alreadyHave to this
        var uploaded = uploaded2.Concat(uploaded3).ToList();

        string output = "@echo off\r\n";
        foreach (Uploadable uploadable in uploaded)
        {
            string path = uploadable.Path.Replace("\\\\", "\\")
                .Replace(@"M:\[IMS][Galgame Vocal MP3 Collection 1996-2006]\", "");
            output += $"del \"{path}\"\r\n";
        }

        await File.WriteAllTextAsync("ggvc_delete_uploaded.bat", output);
    }

    public static async Task ImportGGVC()
    {
        string dir = "M:\\[IMS][Galgame Vocal MP3 Collection 1996-2006]";
        // dir = "M:\\a";
        var regex = new Regex("【(.+)】(?: )?(.+)(?: )?(?:\\[|【)(.*)(?:]|】)", RegexOptions.Compiled);
        string extension = "mp3";

        var songMatches = SongMatcher.ParseSongFile(dir, regex, extension);
        await SongMatcher.Match(songMatches, "C:\\emq\\ggvc4");
    }
}
