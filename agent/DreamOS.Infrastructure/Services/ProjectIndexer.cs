using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;

namespace DreamOS.Infrastructure.Services
{
    public class ProjectIndexer : IProjectIndexer
    {
        private readonly IMemoryEngine _memoryEngine;

        public ProjectIndexer(IMemoryEngine memoryEngine)
        {
            _memoryEngine = memoryEngine;
        }

        public async Task<ProjectContext> IndexProjectAsync(string projectPath)
        {
            var context = new ProjectContext
            {
                ProjectRoot = projectPath,
                ProjectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar))
            };

            if (!Directory.Exists(projectPath))
            {
                return context;
            }

            // 1. Detectar archivos de configuración clave y tecnologías
            await DetectTechnologiesAsync(projectPath, context);

            // 2. Escanear árbol de archivos relevante para desarrollo (excluyendo carpetas ignoradas)
            ScanFileTree(projectPath, projectPath, context.FileTreeSummary);

            // 3. Cargar notas de memoria persistidas por la IA
            context.MemoryNotes = await _memoryEngine.GetSummaryNotesAsync(projectPath);

            return context;
        }

        private async Task DetectTechnologiesAsync(string projectPath, ProjectContext context)
        {
            // Flutter / Dart
            var pubspecPath = Path.Combine(projectPath, "pubspec.yaml");
            if (File.Exists(pubspecPath))
            {
                context.Technologies.Add("Flutter");
                context.Technologies.Add("Dart");
                
                var content = await File.ReadAllTextAsync(pubspecPath);
                context.KeyConfigurations["pubspec.yaml"] = TruncateContent(content, 1000);

                // Detectar arquitectura
                if (content.Contains("flutter_bloc") || content.Contains("bloc:"))
                {
                    context.ArchitectureType = "BLoC (Business Logic Component)";
                }
                else if (content.Contains("flutter_riverpod") || content.Contains("riverpod:"))
                {
                    context.ArchitectureType = "Riverpod State Management";
                }
                else if (content.Contains("provider:"))
                {
                    context.ArchitectureType = "Provider Pattern";
                }
            }

            // NodeJS / JS / TS
            var packageJsonPath = Path.Combine(projectPath, "package.json");
            if (File.Exists(packageJsonPath))
            {
                context.Technologies.Add("NodeJS");
                var content = await File.ReadAllTextAsync(packageJsonPath);
                context.KeyConfigurations["package.json"] = TruncateContent(content, 1000);

                if (content.Contains("\"react\"")) context.Technologies.Add("React");
                if (content.Contains("\"vue\"")) context.Technologies.Add("Vue");
                if (content.Contains("\"next\"")) context.Technologies.Add("NextJS");
                if (content.Contains("\"express\"")) context.Technologies.Add("Express");
            }

            // PHP / Laravel
            var composerJsonPath = Path.Combine(projectPath, "composer.json");
            if (File.Exists(composerJsonPath))
            {
                context.Technologies.Add("PHP");
                var content = await File.ReadAllTextAsync(composerJsonPath);
                context.KeyConfigurations["composer.json"] = TruncateContent(content, 1000);

                if (content.Contains("\"laravel/framework\""))
                {
                    context.Technologies.Add("Laravel");
                    context.ArchitectureType = "MVC (Laravel)";
                }
            }

            // .NET / C#
            var hasCsFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).Any();
            var hasSln = Directory.GetFiles(projectPath, "*.sln", SearchOption.TopDirectoryOnly).Any();
            if (hasCsFiles || hasSln)
            {
                context.Technologies.Add(".NET");
                context.Technologies.Add("C#");
                
                var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories);
                if (csprojFiles.Any())
                {
                    context.KeyConfigurations["csproj"] = Path.GetFileName(csprojFiles[0]);
                    context.ArchitectureType = "Clean Architecture / DDD (.NET)";
                }
            }

            // Entorno .env
            var envPath = Path.Combine(projectPath, ".env");
            if (File.Exists(envPath))
            {
                var content = await File.ReadAllTextAsync(envPath);
                // Ocultar llaves privadas por seguridad al pasar al contexto de la IA
                var safeLines = content.Split('\n')
                    .Select(line => {
                        var idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            var key = line.Substring(0, idx).Trim();
                            return $"{key}=********";
                        }
                        return line;
                    });
                context.KeyConfigurations[".env"] = string.Join("\n", safeLines);
            }
        }

        private void ScanFileTree(string rootPath, string currentPath, List<FileDescriptor> summary, int depth = 0)
        {
            // Limitar profundidad recursiva para no saturar memoria
            if (depth > 6) return;

            var dirInfo = new DirectoryInfo(currentPath);
            if (!dirInfo.Exists) return;

            var ignoredDirectories = new[]
            {
                "node_modules", "build", ".dart_tool", "bin", "obj", ".git", "vendor",
                "packages", "ios", "android", "windows", "macos", "linux", "web",
                "dist", "out", ".idea", ".vscode"
            };

            try
            {
                foreach (var dir in dirInfo.GetDirectories())
                {
                    if (dir.Name.StartsWith(".") || ignoredDirectories.Contains(dir.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    summary.Add(new FileDescriptor
                    {
                        RelativePath = Path.GetRelativePath(rootPath, dir.FullName).Replace('\\', '/'),
                        IsDirectory = true,
                        Size = 0,
                        FileType = "Directory"
                    });

                    // Limitar tamaño total del árbol listado para el contexto de la IA (ej: máx 150 elementos)
                    if (summary.Count > 150) return;

                    ScanFileTree(rootPath, dir.FullName, summary, depth + 1);
                }

                var relevantExtensions = new[]
                {
                    ".dart", ".cs", ".php", ".js", ".ts", ".jsx", ".tsx", ".vue", ".json", 
                    ".html", ".css", ".env", ".xml", ".yaml", ".yml", ".md", ".txt"
                };

                foreach (var file in dirInfo.GetFiles())
                {
                    var ext = file.Extension.ToLower();
                    if (!relevantExtensions.Contains(ext) || file.Name.StartsWith("."))
                    {
                        continue;
                    }

                    summary.Add(new FileDescriptor
                    {
                        RelativePath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/'),
                        IsDirectory = false,
                        Size = file.Length,
                        FileType = ext.TrimStart('.')
                    });

                    if (summary.Count > 150) return;
                }
            }
            catch (Exception)
            {
                // Ignorar directorios con fallos de lectura
            }
        }

        private string TruncateContent(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "\n...[TRUNCADO]";
        }
    }
}
