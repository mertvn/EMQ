using System.Threading.Tasks;
using BlazorApp1.Client;
using BlazorApp1.Client.Pages;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Auth.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorApp1.Client;

public static class Globals
{
    // public static NavigationManager? Navigation { get; set; }

    public static Session? Session { get; set; }

    // [Inject] public static NavigationManager Navigation { get; set; } = default!;
    //
    // public static readonly HubConnection hubConnection = new HubConnectionBuilder()
    //     .WithUrl(new TestNav().ToAbsoluteUri("/GeneralHub"))
    //     .Build();
    //
    // public static bool IsConnected =>
    //     Globals.hubConnection?.State == HubConnectionState.Connected;
    //
    // public static async Task InitGlobals()
    // {
    //     // Navigation = new TestNav();
    //     await Globals.hubConnection.StartAsync();
    // }
    //
    // public static async ValueTask DisposeAsync()
    // {
    //     if (Globals.hubConnection is not null)
    //     {
    //         await Globals.hubConnection.DisposeAsync();
    //     }
    // }
}
