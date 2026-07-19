using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;

namespace DreamOS.Infrastructure.Plugins
{
    public class FlutterPlugin : IProjectPlugin
    {
        private readonly ITerminalService _terminalService;

        public string Name => "Flutter";

        public FlutterPlugin(ITerminalService terminalService)
        {
            _terminalService = terminalService;
        }

        public bool CanHandle(string projectPath)
        {
            return File.Exists(Path.Combine(projectPath, "pubspec.yaml"));
        }

        public async Task<BuildResult> BuildAsync(string projectPath, Action<BuildProgress> onProgress)
        {
            var result = new BuildResult();
            var logBuffer = new List<string>();

            // Phase 1: flutter pub get
            onProgress(new BuildProgress { Percentage = 10, Phase = "Dependencias", LogLine = "Ejecutando flutter pub get..." });
            var pubGetOutput = await _terminalService.ExecuteCommandAsync("flutter pub get", projectPath, line =>
            {
                logBuffer.Add(line);
                onProgress(new BuildProgress { Percentage = 15, Phase = "Dependencias", LogLine = line });
            });

            if (pubGetOutput.Contains("pub get failed"))
            {
                result.IsSuccess = false;
                result.Logs = string.Join("\n", logBuffer);
                result.Errors = await AnalyzeErrorsAsync(pubGetOutput);
                return result;
            }

            // Phase 2: flutter build apk --release
            onProgress(new BuildProgress { Percentage = 30, Phase = "Compilación", LogLine = "Ejecutando flutter build apk --release..." });
            var buildOutput = await _terminalService.ExecuteCommandAsync("flutter build apk --release", projectPath, line =>
            {
                logBuffer.Add(line);
                
                // Parsear porcentaje estimado basado en el log de gradle
                int percent = 30;
                if (line.Contains("Running Gradle task")) percent = 45;
                if (line.Contains("Obtaining application bundle")) percent = 70;
                if (line.Contains("Signing APK")) percent = 85;
                if (line.Contains("Built build")) percent = 95;

                onProgress(new BuildProgress { Percentage = percent, Phase = "Compilación", LogLine = line });
            });

            result.Logs = string.Join("\n", logBuffer);

            if (buildOutput.Contains("Gradle task assembleRelease failed") || buildOutput.Contains("Failed to build"))
            {
                result.IsSuccess = false;
                result.Errors = await AnalyzeErrorsAsync(buildOutput);
                return result;
            }

            // Encontrar el APK generado
            var apkPath = Path.Combine(projectPath, "build", "app", "outputs", "flutter-apk", "app-release.apk");
            if (!File.Exists(apkPath))
            {
                // Fallback para rutas alternativas de gradle
                apkPath = Path.Combine(projectPath, "build", "app", "outputs", "apk", "release", "app-release.apk");
            }

            if (File.Exists(apkPath))
            {
                result.IsSuccess = true;
                result.OutputFilePath = apkPath;
                onProgress(new BuildProgress { Percentage = 100, Phase = "Completado", LogLine = $"Compilación exitosa. APK: {apkPath}" });
            }
            else
            {
                result.IsSuccess = false;
                onProgress(new BuildProgress { Percentage = 100, Phase = "Error", LogLine = "Compilación completada pero no se encontró el archivo APK resultante." });
            }

            return result;
        }

        public async Task<RunResult> RunAsync(string projectPath, Action<BuildProgress> onProgress)
        {
            onProgress(new BuildProgress { Percentage = 10, Phase = "Ejecución", LogLine = "Iniciando flutter run..." });
            
            // Ejecutar en segundo plano
            var runOutput = await _terminalService.ExecuteCommandAsync("flutter run -d chrome --web-renderer canvaskit", projectPath, line =>
            {
                onProgress(new BuildProgress { Percentage = 50, Phase = "Ejecución", LogLine = line });
            });

            var result = new RunResult
            {
                IsRunning = runOutput.Contains("Running on"),
                Logs = runOutput
            };

            return result;
        }

        public Task<List<CompilerError>> AnalyzeErrorsAsync(string buildLogs)
        {
            var errors = new List<CompilerError>();
            if (string.IsNullOrEmpty(buildLogs)) return Task.FromResult(errors);

            // Coincide con errores de Dart: lib/main.dart:12:34: Error: mensaje
            var dartErrorRegex = new Regex(@"([a-zA-Z0-9_\/\\.-]+\.dart):([0-9]+):([0-9]+):\s*(Error|Warning|Info):\s*(.*)", RegexOptions.IgnoreCase);
            
            using var reader = new StringReader(buildLogs);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var match = dartErrorRegex.Match(line);
                if (match.Success)
                {
                    errors.Add(new CompilerError
                    {
                        FilePath = match.Groups[1].Value,
                        Line = int.Parse(match.Groups[2].Value),
                        Column = int.Parse(match.Groups[3].Value),
                        Severity = match.Groups[4].Value,
                        Message = match.Groups[5].Value.Trim()
                    });
                }
                else if (line.Contains("Error:") || line.Contains("Failure"))
                {
                    // Error genérico si no coincide con el formato anterior
                    errors.Add(new CompilerError
                    {
                        Message = line.Trim(),
                        Severity = "Error"
                    });
                }
            }

            return Task.FromResult(errors);
        }
    }
}
