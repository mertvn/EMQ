using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorApp1.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorApp1.Client;

public class idk // todo: find better name
{
    public idk(ILogger<idk> logger, HttpClient client)
    {
        _logger = logger;
        _client = client;
    }

    [Inject] private ILogger<idk> _logger { get; }

    [Inject] private HttpClient _client { get; }

    public async Task<Room?> SyncRoom()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Quiz/SyncRoom", ClientState.Session!.RoomId);
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
                _logger.LogError("Desynchronized");
            }
        }
        else
        {
            // todo warn user and require reload
            _logger.LogError("Desynchronized");
        }

        return null;
    }
}
