using System.Collections.Generic;
using System.Threading.Tasks;

namespace DreamOS.Core.Interfaces
{
    public interface IStorageProvider
    {
        string ProviderName { get; }
        Task<bool> AuthenticateAsync(Action<string> authUrlCallback); // Devuelve true si está autenticado. Si requiere OAuth, llama al callback con la URL.
        Task<string> UploadFileAsync(string localPath, string remoteFolder, string fileName);
        Task<List<string>> ListFilesAsync(string remoteFolder);
    }
}
