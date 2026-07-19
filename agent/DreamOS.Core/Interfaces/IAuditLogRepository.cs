using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;

namespace DreamOS.Core.Interfaces
{
    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog log);
        Task<IEnumerable<AuditLog>> GetAllAsync();
        Task<IEnumerable<AuditLog>> GetByDeviceAsync(string deviceId);
    }
}
