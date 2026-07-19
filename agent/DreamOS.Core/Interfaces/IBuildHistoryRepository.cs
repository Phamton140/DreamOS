using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;

namespace DreamOS.Core.Interfaces
{
    public interface IBuildHistoryRepository
    {
        Task<BuildHistory?> GetByIdAsync(string id);
        Task<IEnumerable<BuildHistory>> GetAllAsync();
        Task<IEnumerable<BuildHistory>> GetByProjectAsync(string projectName);
        Task AddAsync(BuildHistory build);
        Task UpdateAsync(BuildHistory build);
        Task DeleteAsync(string id);
    }
}
