using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DreamOS.Api.Hubs
{
    public class ConsoleHub : Hub
    {
        public async Task JoinProjectGroup(string projectRoot)
        {
            // Vincula el cliente a un grupo para este proyecto específico
            await Groups.AddToGroupAsync(Context.ConnectionId, CleanGroup(projectRoot));
        }

        public async Task LeaveProjectGroup(string projectRoot)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, CleanGroup(projectRoot));
        }

        private string CleanGroup(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
