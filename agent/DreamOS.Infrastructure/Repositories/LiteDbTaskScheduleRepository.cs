using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Repositories
{
    public class LiteDbTaskScheduleRepository : ITaskScheduleRepository
    {
        private readonly LiteDbContext _context;
        private readonly ILiteCollection<TaskSchedule> _collection;

        public LiteDbTaskScheduleRepository(LiteDbContext context)
        {
            _context = context;
            _collection = _context.Database.GetCollection<TaskSchedule>("taskschedules");
        }

        public Task<TaskSchedule?> GetByIdAsync(string id)
        {
            var schedule = _collection.FindById(id);
            return Task.FromResult<TaskSchedule?>(schedule);
        }

        public Task<IEnumerable<TaskSchedule>> GetAllAsync()
        {
            var schedules = _collection.FindAll();
            return Task.FromResult<IEnumerable<TaskSchedule>>(schedules);
        }

        public Task AddAsync(TaskSchedule schedule)
        {
            _collection.Insert(schedule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TaskSchedule schedule)
        {
            _collection.Update(schedule);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            _collection.Delete(id);
            return Task.CompletedTask;
        }
    }
}
