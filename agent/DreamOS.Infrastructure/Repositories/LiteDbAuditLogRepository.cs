using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Repositories
{
    public class LiteDbAuditLogRepository : IAuditLogRepository
    {
        private readonly LiteDbContext _context;
        private readonly ILiteCollection<AuditLog> _collection;

        public LiteDbAuditLogRepository(LiteDbContext context)
        {
            _context = context;
            _collection = _context.Database.GetCollection<AuditLog>("auditlogs");
        }

        public Task AddAsync(AuditLog log)
        {
            _collection.Insert(log);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<AuditLog>> GetAllAsync()
        {
            var logs = _collection.FindAll();
            return Task.FromResult<IEnumerable<AuditLog>>(logs);
        }

        public Task<IEnumerable<AuditLog>> GetByDeviceAsync(string deviceId)
        {
            var logs = _collection.Find(x => x.DeviceId == deviceId);
            return Task.FromResult<IEnumerable<AuditLog>>(logs);
        }
    }
}
