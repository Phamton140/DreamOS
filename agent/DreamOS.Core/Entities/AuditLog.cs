using System;

namespace DreamOS.Core.Entities
{
    public class AuditLog
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // "FileWrite", "CommandExecute", etc.
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Details { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }
}
