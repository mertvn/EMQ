using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace EMQ.Client;

public class ClientUtils // todo: find better name
{
    public ClientUtils(ILogger<ClientUtils> logger, HttpClient client)
    {
        Logger = logger;
        Client = client;
    }

    [Inject] private ILogger<ClientUtils> Logger { get; }

    [Inject] private HttpClient Client { get; }

    public async Task<Room?> SyncRoom()
    {
        HttpResponseMessage res = await Client.PostAsJsonAsync("Quiz/SyncRoom", ClientState.Session!.RoomId);
        if (res.IsSuccessStatusCode)
        {
            Room? room = await res.Content.ReadFromJsonAsync<Room>().ConfigureAwait(false);
            if (room is not null)
            {
                return room;
            }
            else
            {
                // todo warn user and require reload
                Logger.LogError("Desynchronized");
            }
        }
        else
        {
            // todo warn user and require reload
            Logger.LogError("Desynchronized");
        }

        return null;
    }
}
