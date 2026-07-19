using System.Collections.Generic;
using System.Threading.Tasks;

namespace DreamOS.Core.Interfaces
{
    public interface IMemoryEngine
    {
        Task LearnAsync(string projectRoot, string tag, string filePath, string description);
        Task<List<string>> RecallAsync(string projectRoot, string query);
        Task ClearProjectMemoryAsync(string projectRoot);
        Task<string> GetSummaryNotesAsync(string projectRoot);
    }
}
