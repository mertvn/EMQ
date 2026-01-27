using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.VNDB.Business;

public static class MalMethods
{
    private static readonly HttpClient s_client = new()
    {
        DefaultRequestHeaders =
        {
            {
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
            },
            { "Cookie", "MALSESSIONID=7" },
        }
    };

    // Oh joy, more headers
    // Configure twelve things or fail—
    // Web dev paradise
    public static async Task<List<Label>> ProxyGrabPlayerAnimeFromMal(HttpClient client, PlayerVndbInfo vndbInfo)
    {
        var res = await client.PostAsJsonAsync("Auth/ProxyGrabPlayerAnimeFromMal", vndbInfo);
        return res.IsSuccessStatusCode ? (await res.Content.ReadFromJsonAsync<List<Label>>())! : new List<Label>();
    }

    public static async Task<List<Label>> GrabPlayerAnimeFromMal(PlayerVndbInfo vndbInfo)
    {
        var ret = new List<Label>();
        if (!string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            if (vndbInfo.Labels != null && vndbInfo.Labels.Any())
            {
                // Console.WriteLine("GrabPlayerAnimeFromMal labels: " +
                //                   JsonSerializer.Serialize(vndbInfo.Labels, Utils.JsoIndented));

                foreach (var label in vndbInfo.Labels)
                {
                    if (label.Kind is LabelKind.Include or LabelKind.Exclude)
                    {
                        if (!label.IsPrivate || !string.IsNullOrWhiteSpace(vndbInfo.VndbApiToken))
                        {
                            const string apiUrl = "https://myanimelist.net/animelist/";
                            string username = vndbInfo.VndbId;
                            int offset = 0;
                            bool more = true;
                            List<MALUlistAnime> final = new();
                            while (more)
                            {
                                var res = await s_client.GetAsync(
                                    $"{apiUrl}{username}/load.json?offset={offset}&status={label.Id}&order=0");
                                if (res.StatusCode is HttpStatusCode.OK)
                                {
                                    string content = await res.Content.ReadAsStringAsync();
                                    MALUlistAnime[] deser = JsonSerializer.Deserialize<MALUlistAnime[]>(content)!;
                                    final.AddRange(deser);
                                    more = deser.Length >= 300;
                                    if (more)
                                    {
                                        offset += 300;
                                        await Task.Delay(TimeSpan.FromSeconds(3)); // TOSCALE: semaphore
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"{username}: res.StatusCode: {res.StatusCode}");
                                    break;
                                }
                            }

                            if (final.Any())
                            {
                                foreach (var result in final)
                                {
                                    if (result.score <= 0)
                                    {
                                        result.score = null;
                                    }

                                    label.VNs[$"https://myanimelist.net/anime/{result.anime_id!.Value}"] =
                                        (result.score * 10) ?? -1;
                                }

                                ret.Add(label);
                                // Console.WriteLine($"Grabbed {label.VNs.Count} anime for label {label.Id} ({label.Name})");
                            }
                        }
                    }
                }
            }
        }

        return ret;
    }
}

public class MALUlistAnime : MALUlistRoot
{
    public int? anime_id { get; set; }
}

public class MALUlistRoot
{
    public int? score { get; set; }
}
