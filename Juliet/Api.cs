using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Juliet.Model.Param;
using Juliet.Model.Response;
using Juliet.Model.VNDBObject;

namespace Juliet;

public static class Api
{
    private static HttpClient Client { get; } = new() { BaseAddress = new Uri(Constants.VndbApiUrl), };

    // todo: return IResult<T> type with success, message, result properties
    private static async Task<T?> Send<T>(HttpRequestMessage req) where T : class
    {
        try
        {
            // Console.WriteLine($"Sending request {JsonSerializer.Serialize(req)}"); // TODO
            var res = await Client.SendAsync(req);
            // Console.WriteLine("Res: " + JsonSerializer.Serialize(res)); // TODO

            if (res.IsSuccessStatusCode)
            {
                var content = (await res.Content.ReadFromJsonAsync<T>())!;
                return content;
            }
            else
            {
                switch (res.StatusCode)
                {
                    // case HttpStatusCode.BadRequest:
                    //     break;
                    case HttpStatusCode.Unauthorized:
                        // todo auth logic?
                        throw new Exception("Unauthorized.");
                    // case HttpStatusCode.NotFound:
                    //     break;
                    case HttpStatusCode.TooManyRequests:
                        // todo throttling logic?
                        throw new Exception("Throttled.");
                    // case HttpStatusCode.InternalServerError:
                    //     break;
                    // case HttpStatusCode.BadGateway:
                    //     break;
                    default:
                        string str =
                            $"Error communicating with VNDB. Status code: {res.StatusCode:D} {res.StatusCode}, " +
                            $"response content: {await res.Content.ReadAsStringAsync()}";
                        Console.WriteLine(str);
                        throw new Exception(str);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public static async Task<Dictionary<string, User>?> GET_user(Param param)
    {
        if (string.IsNullOrWhiteSpace(param.User))
        {
            return null;
        }

        var req = new HttpRequestMessage
        {
            // todo &fields=lengthvotes,lengthvotes_sum
            RequestUri = new Uri($"user?q={param.User}", UriKind.Relative),
        };

        var res = await Send<Dictionary<string, User>>(req);
        return res;
    }

    public static async Task<ResGET_authinfo?> GET_authinfo(Param param)
    {
        if (string.IsNullOrWhiteSpace(param.APIToken))
        {
            return null;
        }

        var req = new HttpRequestMessage { RequestUri = new Uri("authinfo", UriKind.Relative), };
        req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);

        var res = await Send<ResGET_authinfo>(req);
        return res;
    }

    public static async Task<ResGET_ulist_labels?> GET_ulist_labels(Param param)
    {
        if (string.IsNullOrWhiteSpace(param.User))
        {
            return null;
        }

        // todo &fields=count
        var req = new HttpRequestMessage { RequestUri = new Uri($"ulist_labels?user={param.User}", UriKind.Relative), };

        if (!string.IsNullOrWhiteSpace(param.APIToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);
        }

        var res = await Send<ResGET_ulist_labels>(req);
        return res;
    }

    public static async Task<List<ResPOST<ResPOST_ulist>>?> POST_ulist(ParamPOST_ulist param)
    {
        // todo validate other params
        if (string.IsNullOrWhiteSpace(param.User))
        {
            return null;
        }

        var final = new List<ResPOST<ResPOST_ulist>>();

        string vndbId = "v1"; // pagination by id should be faster for this type of request
        // int page = 0;
        bool more;
        do
        {
            //  page += 1;

            string op = vndbId == "v1" ? ">=" : ">"; // >= causes duplicate entries, but v0 isn't an accepted vndbid zzz
            // TODO
            var dict = new Dictionary<string, object>()
            {
                { "user", param.User },
                { "fields", string.Join(", ", param.Fields.Select(x => x.GetDescription())) },
                { "normalized_filters", param.NormalizedFilters },
                { "compact_filters", param.CompactFilters },
                { "results", param.Exhaust ? Constants.MaxResultsPerPage : param.ResultsPerPage },
                //  { "page", page },
            };

            string filters = "";
            if (param.RawFilters != null)
            {
                // todo
                filters = param.RawFilters;
                dict.Add("filters", filters);
            }
            else
            {
                if (param.Filters != null)
                {
                    filters = param.Filters.ToJsonNormalized(param.Filters, ref filters, true);
                    dict.Add("filters", "[\"and\"," + filters + $",[\"id\",\"{op}\",\"{vndbId}\"]]");
                }
                else
                {
                    dict.Add("filters", $"[\"id\",\"{op}\",\"{vndbId}\"]");
                }
            }

            string json = JsonSerializer.Serialize(dict,
                new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Console.WriteLine("json:" + json);

            var req = new HttpRequestMessage
            {
                RequestUri = new Uri("ulist", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(param.APIToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);
            }

            var res = await Send<ResPOST<ResPOST_ulist>>(req);
            if (res != null)
            {
                Console.WriteLine("normalized filters: " + JsonSerializer.Serialize(res.NormalizedFilters,
                    new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

                final.Add(res);
                more = res.More;
                if (res.More)
                {
                    vndbId = res.Results.Last().Id;
                }
            }
            else
            {
                more = false;
            }
        } while (param.Exhaust && more);

        return final;
    }

    public static async Task<List<ResPOST<ResPOST_vn>>?> POST_vn(ParamPOST_vn param)
    {
        var final = new List<ResPOST<ResPOST_vn>>();

        string vndbId = "v1"; // pagination by id should be faster for this type of request
        // int page = 0;
        bool more;
        do
        {
            //  page += 1;

            string op = vndbId == "v1" ? ">=" : ">"; // >= causes duplicate entries, but v0 isn't an accepted vndbid zzz
            // TODO
            var dict = new Dictionary<string, object>()
            {
                { "fields", string.Join(", ", param.Fields.Select(x => x.GetDescription())) },
                { "normalized_filters", param.NormalizedFilters },
                { "compact_filters", param.CompactFilters },
                { "results", param.Exhaust ? Constants.MaxResultsPerPage : param.ResultsPerPage },
                //  { "page", page },
            };

            if (!string.IsNullOrWhiteSpace(param.User))
            {
                dict.Add("user", param.User);
            }

            string filters = "";
            if (param.RawFilters != null)
            {
                // todo
                filters = param.RawFilters;
                dict.Add("filters", filters);
            }
            else
            {
                if (param.Filters != null)
                {
                    filters = param.Filters.ToJsonNormalized(param.Filters, ref filters, true);
                    dict.Add("filters", "[\"and\"," + filters + $",[\"id\",\"{op}\",\"{vndbId}\"]]");
                }
                else
                {
                    dict.Add("filters", $"[\"id\",\"{op}\",\"{vndbId}\"]");
                }
            }

            string json = JsonSerializer.Serialize(dict,
                new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Console.WriteLine("json:" + json);

            var req = new HttpRequestMessage
            {
                RequestUri = new Uri("vn", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(param.APIToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);
            }

            var res = await Send<ResPOST<ResPOST_vn>>(req);
            if (res != null)
            {
                Console.WriteLine("normalized filters: " + JsonSerializer.Serialize(res.NormalizedFilters,
                    new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

                final.Add(res);
                more = res.More;
                if (res.More)
                {
                    vndbId = res.Results.Last().Id;
                }
            }
            else
            {
                more = false;
            }
        } while (param.Exhaust && more);

        return final;
    }
}
