using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace EMQ.Shared.Core;

public static class MBApi
{
    private const string BaseUrl = "https://musicbrainz.org/ws/2/";

    public static async Task<MBRelease?> GetRelease(HttpClient client, Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            string url = $"{BaseUrl}release/{id}?fmt=json&inc=recordings";
            var res = await client.GetFromJsonAsync<MBRelease>(url);
            // Console.WriteLine(res);
            return res;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public static async Task<MBRecording?> GetRecording(HttpClient client, Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return null;
            }

            string url = $"{BaseUrl}recording/{id}?fmt=json&inc=artists";
            var res = await client.GetFromJsonAsync<MBRecording>(url);
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
