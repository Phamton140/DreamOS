using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Services
{
    public class IaService : IIaProvider
    {
        private readonly LiteDbContext _dbContext;
        private readonly GeminiIaProvider _geminiProvider;
        private readonly OllamaIaProvider _ollamaProvider;

        public string ProviderName => GetActiveProvider().ProviderName;

        public IaService(LiteDbContext dbContext, GeminiIaProvider geminiProvider, OllamaIaProvider ollamaProvider)
        {
            _dbContext = dbContext;
            _geminiProvider = geminiProvider;
            _ollamaProvider = ollamaProvider;
        }

        private IIaProvider GetActiveProvider()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "active_ia_provider"));
            if (doc != null && doc.TryGetValue("value", out var val))
            {
                var providerName = val.AsString;
                if (providerName.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
                {
                    return _ollamaProvider;
                }
            }
            return _geminiProvider; // Gemini por defecto
        }

        public Task<string> AskAsync(string prompt, ProjectContext context)
        {
            return GetActiveProvider().AskAsync(prompt, context);
        }

        public Task<List<FileChange>> ModifyProjectAsync(string prompt, ProjectContext context, List<string> targetFiles)
        {
            return GetActiveProvider().ModifyProjectAsync(prompt, context, targetFiles);
        }
    }
}
