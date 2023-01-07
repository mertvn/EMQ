using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EMQ.Client;

public class ClientUtils
{
    public ClientUtils(ILogger<ClientUtils> logger, HttpClient client, ILocalStorageService localStorage,
        ClientConnectionManager clientConnectionManager)
    {
        Logger = logger;
        Client = client;
        LocalStorage = localStorage;
        ClientConnectionManager = clientConnectionManager;
    }

    [Inject]
    private ILogger<ClientUtils> Logger { get; }

    [Inject]
    private HttpClient Client { get; }

    [Inject]
    private ILocalStorageService LocalStorage { get; }

    [Inject]
    private ClientConnectionManager ClientConnectionManager { get; }

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

    public async Task SaveSessionToLocalStorage()
    {
        string json = JsonSerializer.Serialize(ClientState.Session);
        Logger.LogInformation("saving session: " + json);
        await LocalStorage.SetItemAsync("session", json);
    }

    public async Task TryRestoreSession()
    {
        if (ClientState.Session is null)
        {
            string? sessionStr = await LocalStorage.GetItemAsync<string>("session");
            if (!string.IsNullOrWhiteSpace(sessionStr))
            {
                Session? session = JsonSerializer.Deserialize<Session>(sessionStr);
                if (session != null)
                {
                    Console.WriteLine($"Attempting to restore session with token {session.Token}");

                    HttpResponseMessage res = await Client.PostAsJsonAsync("Auth/ValidateSession", session.Token);
                    if (res.IsSuccessStatusCode)
                    {
                        ClientState.Session = session;
                        await ClientConnectionManager.StartManagingConnection();
                    }
                    else
                    {
                        ClientState.Session = null;
                        await SaveSessionToLocalStorage();
                    }
                }
            }
        }
    }
}
