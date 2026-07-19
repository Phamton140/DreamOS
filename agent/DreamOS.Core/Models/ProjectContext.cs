using System.Collections.Generic;

namespace DreamOS.Core.Models
{
    public class FileDescriptor
    {
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public string FileType { get; set; } = string.Empty; // "Dart", "PHP", "Json", etc.
    }

    public class ProjectContext
    {
        public string ProjectRoot { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public List<string> Technologies { get; set; } = new();
        public string ArchitectureType { get; set; } = "Unknown";
        public Dictionary<string, string> KeyConfigurations { get; set; } = new();
        public List<FileDescriptor> FileTreeSummary { get; set; } = new();
        public string MemoryNotes { get; set; } = string.Empty;
        public string LastBuildStatus { get; set; } = string.Empty;
    }
}
