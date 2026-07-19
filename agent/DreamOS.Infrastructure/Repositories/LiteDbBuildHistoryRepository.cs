using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Repositories
{
    public class LiteDbBuildHistoryRepository : IBuildHistoryRepository
    {
        private readonly LiteDbContext _context;
        private readonly ILiteCollection<BuildHistory> _collection;

        public LiteDbBuildHistoryRepository(LiteDbContext context)
        {
            _context = context;
            _collection = _context.Database.GetCollection<BuildHistory>("builds");
        }

        public Task<BuildHistory?> GetByIdAsync(string id)
        {
            var build = _collection.FindById(id);
            return Task.FromResult<BuildHistory?>(build);
        }

        public Task<IEnumerable<BuildHistory>> GetAllAsync()
        {
            var builds = _collection.FindAll();
            return Task.FromResult<IEnumerable<BuildHistory>>(builds);
        }

        public Task<IEnumerable<BuildHistory>> GetByProjectAsync(string projectName)
        {
            var builds = _collection.Find(x => x.ProjectName == projectName);
            return Task.FromResult<IEnumerable<BuildHistory>>(builds);
        }

        public Task AddAsync(BuildHistory build)
        {
            _collection.Insert(build);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BuildHistory build)
        {
            _collection.Update(build);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            _collection.Delete(id);
            return Task.CompletedTask;
        }
    }
}
