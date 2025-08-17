using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Shared.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace EMQ.Client;

// todo? move
public class Pong
{
    // todo? move
    public static readonly HashSet<string> QuizPages = new() { "QuizPage", "RoomPage", "PyramidPage" };

    public string Page { get; init; } = "";
}

public class ClientConnectionManager
{
    public ClientConnectionManager(ILogger<ClientConnectionManager> logger, HttpClient client)
    {
        Logger = logger;
        Client = client;
    }

    [Inject]
    private ILogger<ClientConnectionManager> Logger { get; }

    [Inject]
    private HttpClient Client { get; set; }

    private Dictionary<string, (Type[] types, Func<object?[], Task> value)> CurrentHandlers { get; set; } = new();

    public static bool IsConnected => ClientState.Session?.hubConnection?.State == HubConnectionState.Connected;

    public async Task StartManagingConnection()
    {
        await EnsureHubConnection(CurrentHandlers);

        // add "global" handlers
        // ClientState.Session!.hubConnection!.On("ReceivePing", OnReceivePing);
        // todo chat stuff etc.
    }

    private async Task InitHubConnection()
    {
        Logger.LogInformation($"InitHubConnection to {Client.BaseAddress}");
        ClientState.Session!.hubConnection = new HubConnectionBuilder()
            .WithUrl(new Nav(Client.BaseAddress!.ToString()).ToAbsoluteUri("/QuizHub"),
                options => { options.AccessTokenProvider = () => Task.FromResult(ClientState.Session.Token)!; })
            .WithAutomaticReconnect()
            .ConfigureLogging(logging =>
            {
                logging.AddProvider(Logger.AsLoggerProvider());
                logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
            })
            .Build();

        await ClientState.Session.hubConnection.StartAsync();
    }

    public async Task StopHubConnection()
    {
        Logger.LogInformation($"StopHubConnection to {Client.BaseAddress}");
        try
        {
            await ClientState.Session!.hubConnection!.StopAsync();
        }
        finally
        {
            await ClientState.Session!.hubConnection!.DisposeAsync();
        }
    }

    private async Task EnsureHubConnection(Dictionary<string, (Type[] types, Func<object?[], Task> value)> handlers)
    {
        if (ClientState.Session!.hubConnection is null ||
            ClientState.Session.hubConnection.State is not
                HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
        {
            await InitHubConnection();
        }

        while (ClientState.Session.hubConnection!.State != HubConnectionState.Connected)
        {
            Logger.LogInformation("waiting 50ms for conn");
            await Task.Delay(50);
        }

        RegisterMethods(handlers);

        var del = async delegate(Exception? exception)
        {
            if (exception != null)
            {
                Logger.LogError(exception.ToString());
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
            await EnsureHubConnection(CurrentHandlers);
        };
        ClientState.Session.hubConnection.Closed -= del;
        ClientState.Session.hubConnection.Closed += del;

        Logger.LogInformation("HubConnectionState=Connected");
    }

    public async Task SetHandlers(Dictionary<string, (Type[] types, Func<object?[], Task> value)> handlers)
    {
        CurrentHandlers.Clear();
        await EnsureHubConnection(handlers);
        CurrentHandlers = handlers;
    }

    public void RegisterMethod(string key, Type[] types, Func<object?[], Task> value)
    {
        // for (int i = 0; i < types.Length; i++)
        // {
        //     Type type = types[i];
        //     Logger.LogWarning($"parameter {i} is {type}");
        // }

        if (!CurrentHandlers.ContainsKey(key)) // todo room and pyramid have conflicting method name
        {
            CurrentHandlers.Add(key, (types, value));
        }

        // TODO: Use the return value and dispose of old handlers
        ClientState.Session!.hubConnection!.On(key, types, value); // todo one method that then dispatches with switch
        Logger.LogInformation("Registered method {Key}", key);
    }

    private void RegisterMethods(Dictionary<string, (Type[] types, Func<object?[], Task> value)> handlers)
    {
        Logger.LogInformation($"Registering {handlers.Count} methods");
        foreach ((string key, (Type[] types, Func<object?[], Task> value)) in handlers)
        {
            // Logger.LogInformation($"CurrentHandlers: {JsonSerializer.Serialize(CurrentHandlers.Select(x => x.Key))}");
            // if (!CurrentHandlers.ContainsKey(key))
            // {
            // Logger.LogInformation($"here2");
            RegisterMethod(key, types, value);
            // }
            // else
            // {
            // Logger.LogInformation($"skipped key: {key}");
            // throw new Exception("Duplicate method register");
            // }
        }
    }
}
