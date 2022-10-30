using System.Threading.Tasks;
using EMQ.Client;
using EMQ.Client.Pages;
using EMQ.Shared.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace EMQ.Client;

public static class ClientState
{
    public static Session? Session { get; set; }
}
