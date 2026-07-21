using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Repositories
{
    public class LiteDbWorkspaceRepository : IWorkspaceRepository
    {
        private readonly LiteDbContext _context;
        private readonly ILiteCollection<Workspace> _collection;

        public LiteDbWorkspaceRepository(LiteDbContext context)
        {
            _context = context;
            _collection = _context.Database.GetCollection<Workspace>("workspaces");
            try
            {
                _collection.EnsureIndex(x => x.Name);
            }
            catch
            {
                // Ignorar excepción de indexación LiteDB si la propiedad es compleja
            }
        }

        public Task<Workspace?> GetByIdAsync(string id)
        {
            var ws = _collection.FindById(id);
            return Task.FromResult<Workspace?>(ws);
        }

        public Task<IEnumerable<Workspace>> GetAllAsync()
        {
            var wsList = _collection.FindAll();
            return Task.FromResult<IEnumerable<Workspace>>(wsList);
        }

        public Task AddAsync(Workspace workspace)
        {
            _collection.Insert(workspace);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Workspace workspace)
        {
            _collection.Update(workspace);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            _collection.Delete(id);
            return Task.CompletedTask;
        }
    }
}
