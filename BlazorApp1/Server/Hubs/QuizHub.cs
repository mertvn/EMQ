using System.Linq;
using System.Threading.Tasks;
using BlazorApp1.Server.Controllers;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp1.Server.Hubs
{
    public class QuizHub : Hub
    {
        public async Task SendPlayerIsReady(int playerId)
        {
            var room = QuizController._rooms.SingleOrDefault(x => x.Id == 78); // todo
            await room.Quiz.OnSendPlayerIsReady();
        }
    }
}
