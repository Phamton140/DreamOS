using System;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;

namespace DreamOS.Infrastructure.Services
{
    public class IaProviderFactory
    {
        private readonly LiteDbContext _dbContext;
        private readonly GeminiIaProvider _geminiProvider;
        private readonly OpenAiIaProvider _openAiProvider;

        public IaProviderFactory(LiteDbContext dbContext, GeminiIaProvider geminiProvider, OpenAiIaProvider openAiProvider)
        {
            _dbContext = dbContext;
            _geminiProvider = geminiProvider;
            _openAiProvider = openAiProvider;
        }

        public IIaProvider GetActiveProvider()
        {
            var collection = _dbContext.Database.GetCollection<AiSettings>("ai_settings");
            var settings = collection.FindById("default");

            if (settings != null && string.Equals(settings.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                return _openAiProvider;
            }

            return _geminiProvider;
        }
    }
}
