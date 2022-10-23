using System.Threading.Tasks;
using BlazorApp1.Client;
using BlazorApp1.Client.Pages;
using BlazorApp1.Shared.Auth;
using BlazorApp1.Shared.Auth.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorApp1.Client;

public static class ClientState
{
    public static Session? Session { get; set; }
}
