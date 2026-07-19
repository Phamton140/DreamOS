using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;

namespace DreamOS.Core.Interfaces
{
    public interface IDeviceRepository
    {
        Task<Device?> GetByIdAsync(string id);
        Task<IEnumerable<Device>> GetAllAsync();
        Task AddAsync(Device device);
        Task UpdateAsync(Device device);
        Task DeleteAsync(string id);
    }
}
