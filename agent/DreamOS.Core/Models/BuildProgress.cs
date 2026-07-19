namespace DreamOS.Core.Models
{
    public class BuildProgress
    {
        public int Percentage { get; set; }
        public string Phase { get; set; } = string.Empty; // "Dependencies", "Compiling", "Packaging", etc.
        public string LogLine { get; set; } = string.Empty;
    }
}
