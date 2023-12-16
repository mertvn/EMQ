using System;
using System.Collections.Concurrent;
using System.Net;
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
        Room? room = null;

        var res = await Client.GetAsync(
            $"Quiz/SyncRoom?token={ClientState.Session?.Token}");

        if (res.StatusCode == HttpStatusCode.NoContent)
            room = null;
        else if (res.IsSuccessStatusCode)
            room = await res.Content.ReadFromJsonAsync<Room>();

        // Console.WriteLine(JsonSerializer.Serialize(room));
        if (room is not null)
        {
            return room;
        }
        else
        {
            // todo warn user and require reload
            Logger.LogError("Failed to SyncRoom");
        }

        return null;
    }

    public async Task SaveSessionToLocalStorage()
    {
        await SaveToLocalStorage("session", ClientState.Session!);
    }

    public async Task SaveToLocalStorage<T>(string key, T value)
    {
        await LocalStorage.SetItemAsync(key, value);
    }

    public async Task<T> LoadFromLocalStorage<T>(string key)
    {
        return await LocalStorage.GetItemAsync<T>(key);
    }

    public async Task TryRestoreSession()
    {
        if (ClientState.Session is null)
        {
            string? sessionStr = await LoadFromLocalStorage<string?>("session");
            if (!string.IsNullOrWhiteSpace(sessionStr) && sessionStr != "null")
            {
                Session? session = JsonSerializer.Deserialize<Session>(sessionStr);
                if (session != null)
                {
                    Console.WriteLine($"Attempting to restore session with token {session.Token}");

                    HttpResponseMessage res = await Client.PostAsJsonAsync("Auth/ValidateSession", session);
                    if (res.IsSuccessStatusCode)
                    {
                        ClientState.Session = await res.Content.ReadFromJsonAsync<Session>();
                        await SaveSessionToLocalStorage();

                        Client.DefaultRequestHeaders.TryAddWithoutValidation(AuthStuff.AuthorizationHeaderName,
                            ClientState.Session!.Token);

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
