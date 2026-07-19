using System;
using System.IO;
using System.Threading.Tasks;
using DreamOS.Api.Hubs;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;
using DreamOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DreamOS.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BuildController : ControllerBase
    {
        private readonly PluginService _pluginService;
        private readonly IHubContext<ConsoleHub> _hubContext;
        private readonly IStorageProvider _storageProvider;
        private readonly IBuildHistoryRepository _buildHistoryRepository;
        private readonly IIaProvider _iaProvider;

        public BuildController(
            PluginService pluginService,
            IHubContext<ConsoleHub> hubContext,
            IStorageProvider storageProvider,
            IBuildHistoryRepository buildHistoryRepository,
            IIaProvider iaProvider)
        {
            _pluginService = pluginService;
            _hubContext = hubContext;
            _storageProvider = storageProvider;
            _buildHistoryRepository = buildHistoryRepository;
            _iaProvider = iaProvider;
        }

        [HttpPost("run-build")]
        public IActionResult StartBuild([FromBody] BuildRequest request)
        {
            if (string.IsNullOrEmpty(request.ProjectPath))
            {
                return BadRequest("La ruta del proyecto es requerida.");
            }

            var plugin = _pluginService.GetPluginForProject(request.ProjectPath);
            if (plugin == null)
            {
                return BadRequest("No se encontró ningún plugin compatible para este proyecto.");
            }

            var buildId = Guid.NewGuid().ToString();

            // Ejecutar en segundo plano
            _ = Task.Run(async () =>
            {
                await ExecuteBackgroundBuildAsync(buildId, request.ProjectPath, plugin);
            });

            return Ok(new { BuildId = buildId, Message = "Compilación iniciada en segundo plano." });
        }

        private async Task ExecuteBackgroundBuildAsync(string buildId, string projectPath, IProjectPlugin plugin)
        {
            var cleanGroup = projectPath.Replace('\\', '/').TrimEnd('/');
            
            var buildRecord = new BuildHistory
            {
                Id = buildId,
                ProjectName = Path.GetFileName(cleanGroup),
                ProjectPath = projectPath,
                Platform = plugin.Name == "Flutter" ? "Android" : "Web/Server",
                Version = "1.0.0",
                BuildNumber = 1,
                BuildDate = DateTime.UtcNow,
                Status = "In Progress"
            };

            await _buildHistoryRepository.AddAsync(buildRecord);

            try
            {
                var result = await plugin.BuildAsync(projectPath, async progress =>
                {
                    // Enviar progreso en tiempo real por SignalR
                    await _hubContext.Clients.Group(cleanGroup).SendAsync("OnBuildProgress", new
                    {
                        BuildId = buildId,
                        Percentage = progress.Percentage,
                        Phase = progress.Phase,
                        LogLine = progress.LogLine
                    });
                });

                buildRecord.Status = result.IsSuccess ? "Success" : "Failed";
                buildRecord.LogPath = ""; // Guardar logs si es necesario en un txt local

                if (result.IsSuccess && !string.IsNullOrEmpty(result.OutputFilePath))
                {
                    // Intentar subir a Storage (Google Drive)
                    try
                    {
                        await _hubContext.Clients.Group(cleanGroup).SendAsync("OnBuildProgress", new
                        {
                            BuildId = buildId,
                            Percentage = 96,
                            Phase = "Distribución",
                            LogLine = "Subiendo archivo a almacenamiento en la nube..."
                        });

                        // Generar changelog con IA
                        var changelog = "Compilación exitosa.";
                        try
                        {
                            var mockContext = new ProjectContext { ProjectName = buildRecord.ProjectName };
                            changelog = await _iaProvider.AskAsync("Genera un changelog muy breve (3 viñetas) para la versión compilada hoy del proyecto.", mockContext);
                        }
                        catch { }

                        var fileName = $"{buildRecord.ProjectName}_v{buildRecord.Version}_{DateTime.Now:yyyyMMddHHmmss}.apk";
                        var fileLink = await _storageProvider.UploadFileAsync(result.OutputFilePath, buildRecord.ProjectName, fileName);

                        buildRecord.OutputArtifactUrl = fileLink;
                        buildRecord.StorageProvider = _storageProvider.ProviderName;
                        buildRecord.Changelog = changelog;

                        await _hubContext.Clients.Group(cleanGroup).SendAsync("OnBuildProgress", new
                        {
                            BuildId = buildId,
                            Percentage = 100,
                            Phase = "Completado",
                            LogLine = $"¡Subida completa! Enlace de descarga: {fileLink}"
                        });
                    }
                    catch (Exception ex)
                    {
                        await _hubContext.Clients.Group(cleanGroup).SendAsync("OnBuildProgress", new
                        {
                            BuildId = buildId,
                            Percentage = 100,
                            Phase = "Completado con advertencias",
                            LogLine = $"Compilación exitosa, pero falló la subida: {ex.Message}"
                        });
                    }
                }
                else
                {
                    var errorsListStr = string.Join("\n- ", result.Errors.ConvertAll(e => $"{e.FilePath}:{e.Line} - {e.Message}"));
                    await _hubContext.Clients.Group(cleanGroup).SendAsync("OnBuildProgress", new
                    {
                        BuildId = buildId,
                        Percentage = 100,
                        Phase = "Fallido",
                        LogLine = $"Errores de compilación detectados:\n- {errorsListStr}"
                    });
                }

                await _buildHistoryRepository.UpdateAsync(buildRecord);
            }
            catch (Exception ex)
            {
                buildRecord.Status = "Failed";
                await _buildHistoryRepository.UpdateAsync(buildRecord);

                await _hubContext.Clients.Group(cleanGroup).SendAsync("OnBuildProgress", new
                {
                    BuildId = buildId,
                    Percentage = 100,
                    Phase = "Error",
                    LogLine = $"Error interno en el servidor durante compilación: {ex.Message}"
                });
            }
        }
    }

    public class BuildRequest
    {
        public string ProjectPath { get; set; } = string.Empty;
    }
}
