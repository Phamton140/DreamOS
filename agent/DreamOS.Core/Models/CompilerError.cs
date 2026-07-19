namespace DreamOS.Core.Models
{
    public class CompilerError
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Error"; // "Warning", "Error", "Info"
    }
}
