using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Services
{
    public class MemoryEntry
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectRoot { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty; // Ej: "Login", "Auth", "Database"
        public string FilePath { get; set; } = string.Empty; // Ruta relativa del archivo
        public string Description { get; set; } = string.Empty; // Explicación de lo que contiene
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MemoryEngine : IMemoryEngine
    {
        private readonly LiteDbContext _context;
        private readonly ILiteCollection<MemoryEntry> _collection;

        public MemoryEngine(LiteDbContext context)
        {
            _context = context;
            _collection = _context.Database.GetCollection<MemoryEntry>("project_memories");
            
            // Indexar campos para búsquedas rápidas
            _collection.EnsureIndex(x => x.ProjectRoot);
            _collection.EnsureIndex(x => x.Tag);
        }

        public Task LearnAsync(string projectRoot, string tag, string filePath, string description)
        {
            var rootClean = CleanPath(projectRoot);
            var fileClean = filePath.Replace('\\', '/');

            // Buscar si ya existe una entrada para esta ruta
            var existing = _collection.FindOne(x => x.ProjectRoot == rootClean && x.FilePath == fileClean);
            if (existing != null)
            {
                existing.Tag = tag;
                existing.Description = description;
                existing.CreatedAt = DateTime.UtcNow;
                _collection.Update(existing);
            }
            else
            {
                var entry = new MemoryEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    ProjectRoot = rootClean,
                    Tag = tag,
                    FilePath = fileClean,
                    Description = description
                };
                _collection.Insert(entry);
            }

            return Task.CompletedTask;
        }

        public Task<List<string>> RecallAsync(string projectRoot, string query)
        {
            var rootClean = CleanPath(projectRoot);
            var results = new List<string>();

            if (string.IsNullOrEmpty(query)) return Task.FromResult(results);

            // Búsqueda simple de texto en Tag o Descripción
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var entries = _collection.Find(x => x.ProjectRoot == rootClean);
            
            foreach (var entry in entries)
            {
                // Coincidencia si alguna palabra clave está en Tag o Descripción
                var matches = queryWords.Any(word => 
                    entry.Tag.Contains(word, StringComparison.OrdinalIgnoreCase) || 
                    entry.Description.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                    entry.FilePath.Contains(word, StringComparison.OrdinalIgnoreCase)
                );

                if (matches)
                {
                    results.Add(entry.FilePath);
                }
            }

            return Task.FromResult(results);
        }

        public Task ClearProjectMemoryAsync(string projectRoot)
        {
            var rootClean = CleanPath(projectRoot);
            _collection.DeleteMany(x => x.ProjectRoot == rootClean);
            return Task.CompletedTask;
        }

        public Task<string> GetSummaryNotesAsync(string projectRoot)
        {
            var rootClean = CleanPath(projectRoot);
            var entries = _collection.Find(x => x.ProjectRoot == rootClean).ToList();

            if (!entries.Any())
            {
                return Task.FromResult("No hay notas previas grabadas en la memoria.");
            }

            var sb = new StringBuilder();
            foreach (var group in entries.GroupBy(x => x.Tag))
            {
                sb.AppendLine($"Tag [{group.Key}]:");
                foreach (var entry in group)
                {
                    sb.AppendLine($"  - {entry.FilePath}: {entry.Description}");
                }
            }

            return Task.FromResult(sb.ToString());
        }

        private string CleanPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }
    }
}
