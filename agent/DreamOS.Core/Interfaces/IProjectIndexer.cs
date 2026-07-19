using System.Threading.Tasks;
using DreamOS.Core.Models;

namespace DreamOS.Core.Interfaces
{
    public interface IProjectIndexer
    {
        Task<ProjectContext> IndexProjectAsync(string projectPath);
    }
}
