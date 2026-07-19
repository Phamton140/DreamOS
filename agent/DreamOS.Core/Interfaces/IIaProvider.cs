using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Models;

namespace DreamOS.Core.Interfaces
{
    public interface IIaProvider
    {
        string ProviderName { get; }
        Task<string> AskAsync(string prompt, ProjectContext context);
        Task<List<FileChange>> ModifyProjectAsync(string prompt, ProjectContext context, List<string> targetFiles);
    }
}
