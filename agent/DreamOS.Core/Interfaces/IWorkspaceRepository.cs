using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;

namespace DreamOS.Core.Interfaces
{
    public interface IWorkspaceRepository
    {
        Task<Workspace?> GetByIdAsync(string id);
        Task<IEnumerable<Workspace>> GetAllAsync();
        Task AddAsync(Workspace workspace);
        Task UpdateAsync(Workspace workspace);
        Task DeleteAsync(string id);
    }
}
