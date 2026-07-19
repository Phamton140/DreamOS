using System;

namespace DreamOS.Core.Entities
{
    public class TaskSchedule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty; // e.g. "0 20 * * *" (8 PM)
        public string TaskType { get; set; } = string.Empty; // "Backup", "Build", "GitCommit"
        public string ProjectPath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty; // JSON or raw string arguments
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
    }
}
