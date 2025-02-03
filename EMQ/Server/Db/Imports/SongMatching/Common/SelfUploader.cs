using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EMQ.Shared.Core;

namespace EMQ.Server.Db.Imports.SongMatching.Common;

public static class SelfUploader
{
    public static async Task<string> Upload(Uploadable uploadable, string extension)
    {
        string guid;
        if (false && !string.IsNullOrEmpty(uploadable.MusicBrainzRecording))
        {
            // bad idea because we could have multiple files for the same recording
            guid = uploadable.MusicBrainzRecording;
        }
        else
        {
            guid = Guid.NewGuid().ToString();
            Console.WriteLine($"assigned {guid} to {uploadable.Path}");
        }

        string newPath = $"N:/a/mb/selfhoststorage/{guid}{extension}";
        if (!File.Exists(newPath))
        {
            File.Copy(uploadable.Path, newPath);
        }

        // todo
        return newPath.Replace("N:/a/mb", "https://emqselfhost");
    }
}
