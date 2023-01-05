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
        var room = await Client.GetFromJsonAsync<Room>($"Quiz/SyncRoom?roomId={ClientState.Session?.RoomId}");
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
