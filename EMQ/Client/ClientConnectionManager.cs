using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace EMQ.Client;

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

    public bool IsConnected =>
        ClientState.Session?.hubConnection?.State == HubConnectionState.Connected;

    public async Task StartManagingConnection()
    {
        await EnsureHubConnection(CurrentHandlers);

        // ClientState.Timer = new Timer { Interval = 3000, AutoReset = true };
        // ClientState.Timer.Elapsed += async (sender, args) => { await EnsureHubConnection(CurrentHandlers); };
        // ClientState.Timer.Start();
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
            Logger.LogInformation("waiting 500ms for conn");
            await Task.Delay(500);
        }

        RegisterMethods(handlers);

        var del = async delegate(Exception? exception)
        {
            if (exception != null)
            {
                Logger.LogError(exception.ToString());
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
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
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            Logger.LogWarning($"parameter {i} is {type}");
        }

        if (!CurrentHandlers.ContainsKey(key))
        {
            CurrentHandlers.Add(key, (types, value));
        }

        ClientState.Session!.hubConnection!.On(key, types, value);
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
