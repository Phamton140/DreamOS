using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;

namespace DreamOS.Infrastructure.Plugins
{
    public class LaravelPlugin : IProjectPlugin
    {
        private readonly ITerminalService _terminalService;

        public string Name => "Laravel";

        public LaravelPlugin(ITerminalService terminalService)
        {
            _terminalService = terminalService;
        }

        public bool CanHandle(string projectPath)
        {
            var composerPath = Path.Combine(projectPath, "composer.json");
            if (!File.Exists(composerPath)) return false;

            try
            {
                var content = File.ReadAllText(composerPath);
                return content.Contains("laravel/framework");
            }
            catch
            {
                return false;
            }
        }

        public async Task<BuildResult> BuildAsync(string projectPath, Action<BuildProgress> onProgress)
        {
            var result = new BuildResult();
            var logBuffer = new List<string>();

            // Phase 1: composer install
            onProgress(new BuildProgress { Percentage = 10, Phase = "Dependencias PHP", LogLine = "Ejecutando composer install..." });
            var composerOutput = await _terminalService.ExecuteCommandAsync("composer install --no-interaction --prefer-dist", projectPath, line =>
            {
                logBuffer.Add(line);
                onProgress(new BuildProgress { Percentage = 20, Phase = "Dependencias PHP", LogLine = line });
            });

            if (composerOutput.Contains("Composer de-activation failed") || composerOutput.Contains("Script php artisan"))
            {
                // Algunas advertencias no fallan el build, pero errores críticos sí.
            }

            // Phase 2: npm install && npm run build (si existe frontend node)
            if (File.Exists(Path.Combine(projectPath, "package.json")))
            {
                onProgress(new BuildProgress { Percentage = 50, Phase = "Dependencias Node", LogLine = "Ejecutando npm install & build..." });
                var npmOutput = await _terminalService.ExecuteCommandAsync("npm install && npm run build", projectPath, line =>
                {
                    logBuffer.Add(line);
                    onProgress(new BuildProgress { Percentage = 70, Phase = "Compilación Frontend", LogLine = line });
                });
            }

            // Phase 3: PHP Artisan optimize
            onProgress(new BuildProgress { Percentage = 80, Phase = "Configuración", LogLine = "Ejecutando php artisan config:clear..." });
            var artisanOutput = await _terminalService.ExecuteCommandAsync("php artisan config:clear && php artisan route:clear", projectPath, line =>
            {
                logBuffer.Add(line);
                onProgress(new BuildProgress { Percentage = 90, Phase = "Configuración", LogLine = line });
            });

            result.Logs = string.Join("\n", logBuffer);

            // Verificar si hay errores fatales en el buffer
            if (result.Logs.Contains("PHP Fatal error") || result.Logs.Contains("ErrorException"))
            {
                result.IsSuccess = false;
                result.Errors = await AnalyzeErrorsAsync(result.Logs);
            }
            else
            {
                result.IsSuccess = true;
                onProgress(new BuildProgress { Percentage = 100, Phase = "Completado", LogLine = "Laravel listo y optimizado correctamente." });
            }

            return result;
        }

        public async Task<RunResult> RunAsync(string projectPath, Action<BuildProgress> onProgress)
        {
            onProgress(new BuildProgress { Percentage = 10, Phase = "Ejecución", LogLine = "Iniciando php artisan serve..." });
            
            var serveOutput = await _terminalService.ExecuteCommandAsync("php artisan serve", projectPath, line =>
            {
                onProgress(new BuildProgress { Percentage = 80, Phase = "Ejecución", LogLine = line });
            });

            var result = new RunResult
            {
                IsRunning = serveOutput.Contains("Server running on"),
                Url = "http://127.0.0.1:8000",
                Logs = serveOutput
            };

            return result;
        }

        public Task<List<CompilerError>> AnalyzeErrorsAsync(string buildLogs)
        {
            var errors = new List<CompilerError>();
            if (string.IsNullOrEmpty(buildLogs)) return Task.FromResult(errors);

            // Coincide con errores de PHP: PHP Fatal error:  [mensaje] in [archivo] on line [linea]
            var phpErrorRegex = new Regex(@"PHP Fatal error:\s*(.*)\s+in\s+(.*)\s+on\s+line\s+([0-9]+)", RegexOptions.IgnoreCase);
            
            using var reader = new StringReader(buildLogs);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var match = phpErrorRegex.Match(line);
                if (match.Success)
                {
                    errors.Add(new CompilerError
                    {
                        Message = match.Groups[1].Value.Trim(),
                        FilePath = match.Groups[2].Value,
                        Line = int.Parse(match.Groups[3].Value),
                        Column = 0,
                        Severity = "Error"
                    });
                }
            }

            return Task.FromResult(errors);
        }
    }
}
