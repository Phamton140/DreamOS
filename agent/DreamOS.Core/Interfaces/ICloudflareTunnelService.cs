using System.Threading.Tasks;

namespace DreamOS.Core.Interfaces
{
    public interface ICloudflareTunnelService
    {
        string TunnelUrl { get; }
        bool IsRunning { get; }
        Task<string> StartTunnelAsync(int localPort);
        Task StopTunnelAsync();
    }
}
