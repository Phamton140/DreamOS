using System;
using System.Collections.Generic;

namespace DreamOS.Core.Entities
{
    public class Workspace
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProjectRootPath { get; set; } = string.Empty;
        public string ActiveGitBranch { get; set; } = string.Empty;
        public string ActiveIaProvider { get; set; } = string.Empty;
        public Dictionary<string, string> EnvVariables { get; set; } = new();
        public Dictionary<string, string> Settings { get; set; } = new();
        public DateTime LastOpened { get; set; } = DateTime.UtcNow;
    }
}
