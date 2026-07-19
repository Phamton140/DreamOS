using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Models;

namespace DreamOS.Core.Interfaces
{
    public interface IFileService
    {
        List<FileDescriptor> ListDirectory(string path);
        Task<string> ReadFileAsync(string path);
        Task WriteFileAsync(string path, string content);
        Task CreateDirectoryAsync(string path);
        Task DeleteFileOrDirectoryAsync(string path);
        Task MoveFileOrDirectoryAsync(string source, string destination);
        Task CopyFileOrDirectoryAsync(string source, string destination);
        bool IsPathSafe(string path);
    }
}
