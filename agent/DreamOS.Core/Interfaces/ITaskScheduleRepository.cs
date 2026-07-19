using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;

namespace DreamOS.Core.Interfaces
{
    public interface ITaskScheduleRepository
    {
        Task<TaskSchedule?> GetByIdAsync(string id);
        Task<IEnumerable<TaskSchedule>> GetAllAsync();
        Task AddAsync(TaskSchedule schedule);
        Task UpdateAsync(TaskSchedule schedule);
        Task DeleteAsync(string id);
    }
}
