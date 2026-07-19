using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Models;

namespace DreamOS.Core.Interfaces
{
    public interface IProjectPlugin
    {
        string Name { get; } // "Flutter", "Laravel", "NodeJS", "DotNet", etc.
        bool CanHandle(string projectPath);
        Task<BuildResult> BuildAsync(string projectPath, Action<BuildProgress> onProgress);
        Task<RunResult> RunAsync(string projectPath, Action<BuildProgress> onProgress);
        Task<List<CompilerError>> AnalyzeErrorsAsync(string buildLogs);
    }
}
