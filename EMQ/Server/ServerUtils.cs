using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace EMQ.Server;

public static class ServerUtils
{
    public static HttpClient Client { get; } =
        new(new HttpClientHandler
        {
            UseProxy = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "DEVELOPMENT"
        }) { DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("EMQ", "7.8") } } };
}
