using System.Threading.Tasks;
using BlazorApp1.Client;
using BlazorApp1.Client.Pages;
using BlazorApp1.Shared.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorApp1.Client;

public static class Globals
{
    public static Session? Session { get; set; }

    // [Inject] public static NavigationManager Navigation { get; set; } = default!;
    //
    // public static readonly HubConnection hubConnection = new HubConnectionBuilder()
    //     .WithUrl(Navigation.ToAbsoluteUri("/GeneralHub"))
    //     .Build();
    //
    // public static bool IsConnected =>
    //     Globals.hubConnection?.State == HubConnectionState.Connected;
    //
    // public static async Task InitGlobals()
    // {
    //
    //     await Globals.hubConnection.StartAsync();
    // }
    //
    //
    // public static async ValueTask DisposeAsync()
    // {
    //     if (Globals.hubConnection is not null)
    //     {
    //         await Globals.hubConnection.DisposeAsync();
    //     }
    // }
}
