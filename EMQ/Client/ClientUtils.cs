using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EMQ.Client;

public class ClientUtils
{
    public ClientUtils(ILogger<ClientUtils> logger, HttpClient client)
    {
        Logger = logger;
        Client = client;
    }

    [Inject]
    private ILogger<ClientUtils> Logger { get; }

    [Inject]
    private HttpClient Client { get; }

    public async Task<Room?> SyncRoom()
    {
        // JsonTypeInfo<Room> typeInfo = SourceGenerationContext.Default.Room ?? throw new InvalidOperationException();

        // Point p1 = new Point(0);
        // Point p2 = new Point(1);
        // Console.WriteLine(JsonSerializer.Serialize(p1));
        // Console.WriteLine(JsonSerializer.Serialize(p2));

        // Assembly.Load("System.Drawing");

        var req = new HttpRequestMessage
        {
            RequestUri = new Uri($"Quiz/SyncRoom?roomId={ClientState.Session!.RoomId.ToString()}", UriKind.Relative)
        };
        HttpResponseMessage res = await Client.SendAsync(req);

        // var room = await Client.GetFromJsonAsync<Room>($"Quiz/SyncRoom?roomId={ClientState.Session?.RoomId}", jsonTypeInfo:typeInfo);
        var room = await Client.GetFromJsonAsync<Room>($"Quiz/SyncRoom?roomId={ClientState.Session?.RoomId}");
        // Room? room = JsonConvert.DeserializeObject<Room?>(await res.Content.ReadAsStringAsync(),
        //     new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });

        // Console.WriteLine(JsonSerializer.Serialize(room));

        if (room is not null)
        {
            return room;
        }
        else
        {
            // todo warn user and require reload
            Logger.LogError("Desynchronized");
        }

        return null;
    }
}
