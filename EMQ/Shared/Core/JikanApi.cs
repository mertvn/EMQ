using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace EMQ.Shared.Core;

public static class JikanApi
{
    private const string BaseUrl = "https://api.jikan.moe/v4/";

    public static async Task<JikanRoot?> GetAnime(HttpClient client, int id)
    {
        try
        {
            if (id <= 0)
            {
                return null;
            }

            string url = $"{BaseUrl}anime/{id}";
            var res = await client.GetFromJsonAsync<JikanRoot>(url);
            // Console.WriteLine(res);
            return res;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
}
