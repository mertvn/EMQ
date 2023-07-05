using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Juliet.Model.Filters;
using Juliet.Model.Param;
using Juliet.Model.Response;
using Juliet.Model.VNDBObject;
using Juliet.Model.VNDBObject.Fields;

namespace Juliet;

// todo return IEnumerable everywhere
public static class Api
{
    private static HttpClient Client { get; } = new() { BaseAddress = new Uri(Constants.VndbApiUrl), };

    // todo: return IResult<T> type with success, message, result properties
    private static async Task<T?> Send<T>(HttpRequestMessage req) where T : class
    {
        try
        {
            // Console.WriteLine($"Sending request {JsonSerializer.Serialize(req)}"); // TODO

            // header ‘user-agent’ is not allowed according to header ‘Access-Control-Allow-Headers’ from CORS preflight response
            // req.Headers.Add("User-Agent", Constants.UserAgent);

            var res = await Client.SendAsync(req);
            // Console.WriteLine("Res: " + JsonSerializer.Serialize(res)); // TODO

            if (res.IsSuccessStatusCode)
            {
                string str = await res.Content.ReadAsStringAsync();
                if (str == "")
                {
                    return default;
                }

                var content = JsonSerializer.Deserialize<T>(str)!;
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

        return await POST_Generic<FieldPOST_ulist, ResPOST_ulist>(param, new Uri("ulist", UriKind.Relative));
    }

    public static async Task<List<ResPOST<ResPOST_vn>>?> POST_vn(ParamPOST_vn param)
    {
        return await POST_Generic<FieldPOST_vn, ResPOST_vn>(param, new Uri("vn", UriKind.Relative));
    }

    public static async Task<List<ResPOST<ResPOST_release>>?> POST_release(ParamPOST_release param)
    {
        return await POST_Generic<FieldPOST_release, ResPOST_release>(param, new Uri("release", UriKind.Relative));
    }

    public static async Task PATCH_ulist(ParamPATCH_ulist param)
    {
        var requestUri = new Uri($"ulist/{param.Id}", UriKind.Relative);

        var json = JsonSerializer.Serialize(param,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Console.WriteLine("json:" + json);

        var req = new HttpRequestMessage
        {
            RequestUri = requestUri,
            Method = HttpMethod.Patch,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(param.APIToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);
        }

        await Send<object>(req);
    }

    private static async Task<List<ResPOST<TReturn>>?> POST_Generic<TParam, TReturn>(
        ParamPOST<TParam> param,
        Uri requestUri)
    {
        var final = new List<ResPOST<TReturn>>();

        int page = 0;
        bool more;
        do
        {
            page += 1;
            var dict = new Dictionary<string, object>()
            {
                { "fields", string.Join(", ", param.Fields.Select(x => ((Enum)(object)x!).GetDescription())) },
                { "normalized_filters", param.NormalizedFilters },
                { "compact_filters", param.CompactFilters },
                { "results", param.Exhaust ? Constants.MaxResultsPerPage : param.ResultsPerPage },
            };

            if (!string.IsNullOrWhiteSpace(param.User))
            {
                dict.Add("user", param.User);
            }

            string filters;
            if (param.RawFilters != null)
            {
                filters = param.RawFilters;
                dict.Add("filters", filters);
                dict.Add("page", page);
            }
            else
            {
                dict.Add("page", page);

                if (param.Filters != null)
                {
                    filters = Query.ToJson(param.Filters, true);
                    dict.Add("filters", filters);
                }
            }

            string json = JsonSerializer.Serialize(dict,
                new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Console.WriteLine("json:" + json);

            var req = new HttpRequestMessage
            {
                RequestUri = requestUri,
                Method = HttpMethod.Post,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(param.APIToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);
            }

            var res = await Send<ResPOST<TReturn>>(req);
            if (res != null)
            {
                Console.WriteLine("normalized filters: " + JsonSerializer.Serialize(res.NormalizedFilters,
                    new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

                final.Add(res);
                more = res.More;
            }
            else
            {
                more = false;
            }
        } while (param.Exhaust && more);

        return final;
    }
}
