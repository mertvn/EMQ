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
    public static async Task<string> Upload(Uploadable uploadable)
    {
        string guid;
        if (false && !string.IsNullOrEmpty(uploadable.MusicBrainzRecording))
        {
            guid = uploadable.MusicBrainzRecording;
        }
        else
        {
            guid = Guid.NewGuid().ToString();
            Console.WriteLine($"assigned {guid} to {uploadable.Path}");
        }

        string newPath = $"M:/a/mb/selfhoststorage/{guid}.mp3";
        if (!File.Exists(newPath))
        {
            File.Copy(uploadable.Path, newPath);
        }

        // todo
        return newPath.Replace("M:/a/mb", "https://emqselfhost");
    }
}
