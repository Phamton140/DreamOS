using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using LiteDB;

namespace DreamOS.Infrastructure.Services
{
    public class StorageService : IStorageProvider
    {
        private readonly LiteDbContext _dbContext;
        private readonly GoogleDriveStorageProvider _googleDriveProvider;

        public string ProviderName => GetActiveProvider().ProviderName;

        public StorageService(LiteDbContext dbContext, GoogleDriveStorageProvider googleDriveProvider)
        {
            _dbContext = dbContext;
            _googleDriveProvider = googleDriveProvider;
        }

        private IStorageProvider GetActiveProvider()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "active_storage_provider"));
            if (doc != null && doc.TryGetValue("value", out var val))
            {
                var providerName = val.AsString;
                if (providerName.Equals("GoogleDrive", StringComparison.OrdinalIgnoreCase))
                {
                    return _googleDriveProvider;
                }
            }
            return _googleDriveProvider; // Default
        }

        public Task<bool> AuthenticateAsync(Action<string> authUrlCallback)
        {
            return GetActiveProvider().AuthenticateAsync(authUrlCallback);
        }

        public Task<string> UploadFileAsync(string localPath, string remoteFolder, string fileName)
        {
            return GetActiveProvider().UploadFileAsync(localPath, remoteFolder, fileName);
        }

        public Task<List<string>> ListFilesAsync(string remoteFolder)
        {
            return GetActiveProvider().ListFilesAsync(remoteFolder);
        }
    }
}
