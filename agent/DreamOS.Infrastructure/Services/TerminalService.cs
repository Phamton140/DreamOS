using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;

namespace DreamOS.Infrastructure.Services
{
    public class TerminalService : ITerminalService
    {
        public async Task<string> ExecuteCommandAsync(string command, string workingDirectory, Action<string>? onOutputReceived = null)
        {
            var outputBuilder = new StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{command}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    onOutputReceived?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    onOutputReceived?.Invoke(e.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error al ejecutar comando: {ex.Message}";
                outputBuilder.AppendLine(errorMsg);
                onOutputReceived?.Invoke(errorMsg);
            }

            return outputBuilder.ToString();
        }
    }
}
