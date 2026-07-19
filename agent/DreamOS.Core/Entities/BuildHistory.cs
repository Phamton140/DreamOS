using System;

namespace DreamOS.Core.Entities
{
    public class BuildHistory
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int BuildNumber { get; set; }
        public DateTime BuildDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = string.Empty; // Success, Failed
        public string LogPath { get; set; } = string.Empty;
        public string OutputArtifactUrl { get; set; } = string.Empty; // URL de descarga (Google Drive, S3, etc.)
        public string StorageProvider { get; set; } = string.Empty; // GoogleDrive, S3, OneDrive, etc.
        public string Changelog { get; set; } = string.Empty;
    }
}
