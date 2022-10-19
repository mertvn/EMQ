using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorApp1.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BlazorApp1.Server.Hubs
{
    public class QuizHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            var session =
                AuthController.Sessions.Find(x => Context.GetHttpContext().Request.QueryString.Value.Contains(x.Token));
            if (session != null)
            {
                session.ConnectionId = Context.ConnectionId;
            }
            else
            {
                throw new Exception();
            }

            return base.OnConnectedAsync();
        }

        // [Authorize]
        public async Task SendPlayerIsReady(int playerId)
        {
            var session = AuthController.Sessions.Find(x => x.ConnectionId == Context.ConnectionId);
            if (session != null)
            {
                var room = QuizController.Rooms.SingleOrDefault(x => x.Quiz.Players.Any(y => y.Id == session.PlayerId));
                if (room?.Quiz != null)
                {
                    await room.Quiz.OnSendPlayerIsReady(session.PlayerId);
                }
                else
                {
                    // todo
                }
            }
            else
            {
                // todo
            }
        }
    }
}
