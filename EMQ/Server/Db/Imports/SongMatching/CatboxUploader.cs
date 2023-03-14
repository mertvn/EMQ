using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace EMQ.Server.Db.Imports.SongMatching;

public static class CatboxUploader
{
    private const string ApiUrl = "https://catbox.moe/user/api.php";

    public static async Task<string> Upload(Uploadable uploadable)
    {
        Dictionary<string, string> strings = new() { { "reqtype", "fileupload" } };

        // Japanese in filenames causes links to be generated without file extensions (???)
        HttpResponseMessage result = await PostMultipart(ApiUrl, strings, uploadable.Path, "a.mp3");
        return await result.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PostMultipart(string url, Dictionary<string, string> strings,
        string filepath, string filename)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        ServicePointManager.Expect100Continue = false;

        MultipartFormDataContent formContent = new();
        foreach (string key in strings.Keys)
        {
            string content = strings[key];
            formContent.Add(new StringContent(content), key);
        }

        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filepath));
        formContent.Add(fileContent, "fileToUpload", filename);

        HttpResponseMessage response = await new HttpClient
        {
            DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("a", "1") } },
            Timeout = TimeSpan.FromSeconds(200)
        }.PostAsync(url, formContent);
        return response;
    }
}
