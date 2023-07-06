using System.Net;

namespace Juliet;

// We have to use something like this because reusing HttpRequestMessages is not allowed.
public class ThrottleHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage res;

        do
        {
            res = await base.SendAsync(request, cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                switch (res.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        throw new Exception("Unauthorized.");
                    case HttpStatusCode.TooManyRequests:
                        if (Settings.WaitWhenThrottled)
                        {
                            Console.WriteLine("Throttled. Waiting for 4 seconds and retrying the request.");
                            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
                        }
                        else
                        {
                            throw new Exception("Throttled.");
                        }

                        break;
                    default:
                        string str =
                            $"Error communicating with VNDB. Status code: {res.StatusCode:D} {res.StatusCode}, " +
                            $"response content: {await res.Content.ReadAsStringAsync(cancellationToken)}";
                        Console.WriteLine(str);
                        throw new Exception(str);
                }
            }
        } while (!res.IsSuccessStatusCode && !cancellationToken.IsCancellationRequested);

        return res;
    }
}
