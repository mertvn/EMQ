﻿using System.Net;
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
    private static ThrottleHandler ThrottleHandler { get; } = new() { InnerHandler = new HttpClientHandler() };

    private static HttpClient Client { get; } = new(ThrottleHandler) { BaseAddress = new Uri(Constants.VndbApiUrl), };

    // todo: return IResult<T> type with success, message, result properties
    private static async Task<T?> Send<T>(HttpRequestMessage req, CancellationToken? cancellationToken = null)
        where T : class
    {
        try
        {
            // Console.WriteLine($"Sending request {JsonSerializer.Serialize(req)}"); // TODO

            // header ‘user-agent’ is not allowed according to header ‘Access-Control-Allow-Headers’ from CORS preflight response
            // req.Headers.Add("User-Agent", Constants.UserAgent);

            var res = await Client.SendAsync(req, cancellationToken ?? CancellationToken.None);
            // Console.WriteLine("Res: " + JsonSerializer.Serialize(res)); // TODO

            // Exception will be thrown by the ThrottleHandler if the request is not successful,
            // so we don't have to check it here.
            string str = await res.Content.ReadAsStringAsync();
            if (str == "")
            {
                return default;
            }

            var content = JsonSerializer.Deserialize<T>(str)!;
            return content;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public static async Task<Dictionary<string, User>?> GET_user(Param param,
        CancellationToken? cancellationToken = null)
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

        var res = await Send<Dictionary<string, User>>(req, cancellationToken);
        return res;
    }

    public static async Task<ResGET_authinfo?> GET_authinfo(Param param, CancellationToken? cancellationToken = null)
    {
        if (string.IsNullOrWhiteSpace(param.APIToken))
        {
            return null;
        }

        var req = new HttpRequestMessage { RequestUri = new Uri("authinfo", UriKind.Relative), };
        req.Headers.Authorization = new AuthenticationHeaderValue("token", param.APIToken);

        var res = await Send<ResGET_authinfo>(req, cancellationToken);
        return res;
    }

    public static async Task<ResGET_ulist_labels?> GET_ulist_labels(Param param,
        CancellationToken? cancellationToken = null)
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

        var res = await Send<ResGET_ulist_labels>(req, cancellationToken);
        return res;
    }

    public static async Task<List<ResPOST<ResPOST_ulist>>?> POST_ulist(ParamPOST_ulist param,
        CancellationToken? cancellationToken = null)
    {
        // todo validate other params
        if (string.IsNullOrWhiteSpace(param.User))
        {
            return null;
        }

        return await POST_Generic<FieldPOST_ulist, ResPOST_ulist>(param, new Uri("ulist", UriKind.Relative),
            cancellationToken);
    }

    public static async Task<List<ResPOST<ResPOST_vn>>?> POST_vn(ParamPOST_vn param,
        CancellationToken? cancellationToken = null)
    {
        return await POST_Generic<FieldPOST_vn, ResPOST_vn>(param, new Uri("vn", UriKind.Relative), cancellationToken);
    }

    public static async Task<List<ResPOST<ResPOST_release>>?> POST_release(ParamPOST_release param,
        CancellationToken? cancellationToken = null)
    {
        return await POST_Generic<FieldPOST_release, ResPOST_release>(param, new Uri("release", UriKind.Relative),
            cancellationToken);
    }

    public static async Task<List<ResPOST<ResPOST_producer>>?> POST_producer(ParamPOST_producer param,
        CancellationToken? cancellationToken = null)
    {
        return await POST_Generic<FieldPOST_producer, ResPOST_producer>(param, new Uri("producer", UriKind.Relative),
            cancellationToken);
    }

    public static async Task<List<ResPOST<ResPOST_character>>?> POST_character(ParamPOST_character param,
        CancellationToken? cancellationToken = null)
    {
        return await POST_Generic<FieldPOST_character, ResPOST_character>(param, new Uri("character", UriKind.Relative),
            cancellationToken);
    }

    public static async Task<List<ResPOST<ResPOST_staff>>?> POST_staff(ParamPOST_staff param,
        CancellationToken? cancellationToken = null)
    {
        return await POST_Generic<FieldPOST_staff, ResPOST_staff>(param, new Uri("staff", UriKind.Relative),
            cancellationToken);
    }

    public static async Task PATCH_ulist(ParamPATCH_ulist param, CancellationToken? cancellationToken = null)
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

        await Send<object>(req, cancellationToken);
    }

    public static async Task PATCH_rlist(ParamPATCH_rlist param, CancellationToken? cancellationToken = null)
    {
        var requestUri = new Uri($"rlist/{param.Id}", UriKind.Relative);

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

        await Send<object>(req, cancellationToken);
    }

    private static async Task<List<ResPOST<TReturn>>?> POST_Generic<TParam, TReturn>(ParamPOST<TParam> param,
        Uri requestUri, CancellationToken? cancellationToken = null)
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

            var res = await Send<ResPOST<TReturn>>(req, cancellationToken);
            if (res != null)
            {
                // Console.WriteLine("normalized filters: " + JsonSerializer.Serialize(res.NormalizedFilters,
                //     new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

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
