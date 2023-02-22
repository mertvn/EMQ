using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace EMQ.Server.Db.Imports;

public static class CatboxUploader
{
    private const string ApiUrl = "https://catbox.moe/user/api.php";

    private static readonly HttpClient s_client = new(new HttpClientHandler { UseProxy = false })
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("apparentlyweneedthis", "6.9") } }
    };

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

        HttpResponseMessage response = await s_client.PostAsync(url, formContent);
        return response;
    }
}
