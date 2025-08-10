using Microsoft.AspNetCore.SignalR;

namespace Programmin2_classroom.Server.Hubs
{
    public class ChatHub : Hub
    {
        public async Task NotifyUpdate()
        {
            await Clients.All.SendAsync("NotifyUpdate");
        }
    }
}