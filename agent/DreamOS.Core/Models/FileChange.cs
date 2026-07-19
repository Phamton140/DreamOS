namespace DreamOS.Core.Models
{
    public class FileChange
    {
        public string FilePath { get; set; } = string.Empty; // Ruta relativa al proyecto o absoluta
        public string Action { get; set; } = string.Empty; // "Create", "Modify", "Delete"
        public string OriginalContent { get; set; } = string.Empty;
        public string NewContent { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; // Descripción de lo que hace el cambio
    }
}
