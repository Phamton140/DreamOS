using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Repositories
{
    public class LiteDbDeviceRepository : IDeviceRepository
    {
        private readonly LiteDbContext _context;
        private readonly ILiteCollection<Device> _collection;

        public LiteDbDeviceRepository(LiteDbContext context)
        {
            _context = context;
            _collection = _context.Database.GetCollection<Device>("devices");
        }

        public Task<Device?> GetByIdAsync(string id)
        {
            var device = _collection.FindById(id);
            return Task.FromResult<Device?>(device);
        }

        public Task<IEnumerable<Device>> GetAllAsync()
        {
            var devices = _collection.FindAll();
            return Task.FromResult<IEnumerable<Device>>(devices);
        }

        public Task AddAsync(Device device)
        {
            _collection.Insert(device);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Device device)
        {
            _collection.Update(device);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            _collection.Delete(id);
            return Task.CompletedTask;
        }
    }
}
