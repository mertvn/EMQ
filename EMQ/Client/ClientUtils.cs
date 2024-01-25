using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using EMQ.Client.Components;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EMQ.Client;

public class ClientUtils
{
    public ClientUtils(ILogger<ClientUtils> logger, HttpClient client, ILocalStorageService localStorage,
        ClientConnectionManager clientConnectionManager
        // ,PlayerPreferencesComponent playerPreferencesComponent
    )
    {
        Logger = logger;
        Client = client;
        LocalStorage = localStorage;
        ClientConnectionManager = clientConnectionManager;
        // PlayerPreferencesComponent = playerPreferencesComponent;
    }

    [Inject]
    private ILogger<ClientUtils> Logger { get; }

    // do NOT use this for communicating with external websites because it contains user's token by default
    [Inject]
    private HttpClient Client { get; }

    [Inject]
    private ILocalStorageService LocalStorage { get; }

    [Inject]
    private ClientConnectionManager ClientConnectionManager { get; }

    // [Inject]
    // private PlayerPreferencesComponent PlayerPreferencesComponent { get; }

    private bool IsRestoringSession { get; set; }

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
        Console.WriteLine("saving session to local storage");
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
        if (ClientState.Session is null && !IsRestoringSession)
        {
            IsRestoringSession = true;
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
                        var content = (await res.Content.ReadFromJsonAsync<ResValidateSession>())!;
                        ClientState.Session = content.Session;
                        ClientState.VndbInfo = content.VndbInfo;

                        Client.DefaultRequestHeaders.TryAddWithoutValidation(AuthStuff.AuthorizationHeaderName,
                            ClientState.Session!.Token);
                        //await PlayerPreferencesComponent.GetVndbInfoFromServer(ClientState.VndbInfo);
                        await SaveSessionToLocalStorage();

                        await ClientConnectionManager.StartManagingConnection();
                    }
                    else
                    {
                        ClientState.Session = null;
                        await SaveSessionToLocalStorage();
                    }
                }
            }

            IsRestoringSession = false;
        }
    }

    public static async Task SendPostFileReq(HttpClient client, UploadResult uploadResult, IBrowserFile file, int mId)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream(UploadConstants.MaxFilesizeBytes));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "\"files\"", $"{mId};{file.Name}");

            // await Task.Delay(TimeSpan.FromSeconds(1));
            var response = await client.PostAsync("Upload/PostFile", content);
            if (response.IsSuccessStatusCode)
            {
                var res = await response.Content.ReadFromJsonAsync<UploadResult>();
                if (res is not null)
                {
                    uploadResult.Uploaded = res.Uploaded;
                    uploadResult.FileName = res.FileName;
                    uploadResult.ResultUrl = res.ResultUrl;
                    uploadResult.ErrorStr = res.ErrorStr;
                    uploadResult.ExtractedResultUrl = res.ExtractedResultUrl;
                }
                else
                {
                    uploadResult.ErrorStr = "UploadResult was null";
                }
            }
            else
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.TooManyRequests:
                        uploadResult.ErrorStr =
                            $"You have been rate-limited. Try again in {response.Headers.RetryAfter} seconds.";
                        break;
                    default:
                        uploadResult.ErrorStr = "Something went wrong when uploading.";
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            uploadResult.ErrorStr = $"Client-side exception while uploading: {ex}";
        }
    }
}
