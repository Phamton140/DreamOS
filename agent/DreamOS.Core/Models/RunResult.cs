namespace DreamOS.Core.Models
{
    public class RunResult
    {
        public bool IsRunning { get; set; }
        public int ProcessId { get; set; }
        public string Url { get; set; } = string.Empty; // Si levanta un puerto web (ej: localhost:8000)
        public string Logs { get; set; } = string.Empty;
    }
}
