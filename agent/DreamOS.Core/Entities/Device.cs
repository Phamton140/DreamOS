using System;

namespace DreamOS.Core.Entities
{
    public class Device
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Os { get; set; } = string.Empty;
        public string TokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        public bool IsRevoked { get; set; } = false;
    }
}
