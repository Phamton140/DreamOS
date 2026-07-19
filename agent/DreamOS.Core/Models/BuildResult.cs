using System.Collections.Generic;

namespace DreamOS.Core.Models
{
    public class BuildResult
    {
        public bool IsSuccess { get; set; }
        public List<CompilerError> Errors { get; set; } = new();
        public string OutputFilePath { get; set; } = string.Empty; // Ruta al ejecutable o APK compilado
        public string Logs { get; set; } = string.Empty;
    }
}
