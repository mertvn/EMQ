using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp1.Server.Hubs
{
    public class GeneralHub : Hub
    {
        // public async Task SendPhaseChanged(string phase)
        // {
        //     await Clients.All.SendAsync("ReceivePhaseChanged", phase);
        // }
    }
}
