using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;

namespace DreamOS.Infrastructure.Services
{
    public class FileService : IFileService
    {
        public bool IsPathSafe(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;

                var fullPath = Path.GetFullPath(path);
                
                // Evitar acceder a directorios del sistema de Windows
                var forbiddenDirectories = new[]
                {
                    @"C:\Windows",
                    @"C:\Program Files",
                    @"C:\Program Files (x86)",
                    @"C:\System Volume Information",
                    @"C:\$Recycle.Bin",
                    @"C:\Users\All Users"
                };

                foreach (var forbidden in forbiddenDirectories)
                {
                    if (fullPath.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<FileDescriptor> ListDirectory(string path)
        {
            if (!IsPathSafe(path))
            {
                throw new UnauthorizedAccessException("Acceso denegado a este directorio por políticas de seguridad.");
            }

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"El directorio '{path}' no existe.");
            }

            var descriptors = new List<FileDescriptor>();
            var dirInfo = new DirectoryInfo(path);

            foreach (var dir in dirInfo.GetDirectories())
            {
                // Ignorar carpetas ocultas y pesadas
                if (dir.Name.StartsWith(".") || dir.Name.Equals("node_modules") || dir.Name.Equals("build") || dir.Name.Equals(".dart_tool"))
                {
                    continue;
                }

                descriptors.Add(new FileDescriptor
                {
                    RelativePath = dir.FullName,
                    IsDirectory = true,
                    Size = 0,
                    FileType = "Directory"
                });
            }

            foreach (var file in dirInfo.GetFiles())
            {
                // Ignorar archivos ocultos
                if (file.Name.StartsWith("."))
                {
                    continue;
                }

                descriptors.Add(new FileDescriptor
                {
                    RelativePath = file.FullName,
                    IsDirectory = false,
                    Size = file.Length,
                    FileType = Path.GetExtension(file.Name).TrimStart('.').ToUpper()
                });
            }

            return descriptors;
        }

        public async Task<string> ReadFileAsync(string path)
        {
            if (!IsPathSafe(path))
            {
                throw new UnauthorizedAccessException("Acceso denegado a este archivo por políticas de seguridad.");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"El archivo '{path}' no existe.");
            }

            return await File.ReadAllTextAsync(path);
        }

        public async Task WriteFileAsync(string path, string content)
        {
            if (!IsPathSafe(path))
            {
                throw new UnauthorizedAccessException("Acceso denegado por políticas de seguridad.");
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, content);
        }

        public Task CreateDirectoryAsync(string path)
        {
            if (!IsPathSafe(path))
            {
                throw new UnauthorizedAccessException("Acceso denegado por políticas de seguridad.");
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return Task.CompletedTask;
        }

        public Task DeleteFileOrDirectoryAsync(string path)
        {
            if (!IsPathSafe(path))
            {
                throw new UnauthorizedAccessException("Acceso denegado por políticas de seguridad.");
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            return Task.CompletedTask;
        }

        public Task MoveFileOrDirectoryAsync(string source, string destination)
        {
            if (!IsPathSafe(source) || !IsPathSafe(destination))
            {
                throw new UnauthorizedAccessException("Acceso denegado por políticas de seguridad.");
            }

            if (File.Exists(source))
            {
                File.Move(source, destination, true);
            }
            else if (Directory.Exists(source))
            {
                Directory.Move(source, destination);
            }

            return Task.CompletedTask;
        }

        public Task CopyFileOrDirectoryAsync(string source, string destination)
        {
            if (!IsPathSafe(source) || !IsPathSafe(destination))
            {
                throw new UnauthorizedAccessException("Acceso denegado por políticas de seguridad.");
            }

            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
            }
            else if (Directory.Exists(source))
            {
                CopyDirectoryRecursive(source, destination);
            }

            return Task.CompletedTask;
        }

        private void CopyDirectoryRecursive(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                var targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (var subDir in dir.GetDirectories())
            {
                var targetSubDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryRecursive(subDir.FullName, targetSubDir);
            }
        }
    }
}
