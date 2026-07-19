using System;
using System.Threading.Tasks;

namespace DreamOS.Core.Interfaces
{
    public interface ITerminalService
    {
        Task<string> ExecuteCommandAsync(string command, string workingDirectory, Action<string>? onOutputReceived = null);
    }
}
