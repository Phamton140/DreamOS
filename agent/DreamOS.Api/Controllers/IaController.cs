using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;
using DreamOS.Infrastructure.Data;
using DreamOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DreamOS.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class IaController : ControllerBase
    {
        private readonly IaProviderFactory _providerFactory;
        private readonly IProjectIndexer _indexer;
        private readonly IFileService _fileService;
        private readonly IMemoryEngine _memoryEngine;
        private readonly LiteDbContext _dbContext;

        public IaController(
            IaProviderFactory providerFactory,
            IProjectIndexer indexer,
            IFileService fileService,
            IMemoryEngine memoryEngine,
            LiteDbContext dbContext)
        {
            _providerFactory = providerFactory;
            _indexer = indexer;
            _fileService = fileService;
            _memoryEngine = memoryEngine;
            _dbContext = dbContext;
        }

        [AllowAnonymous]
        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            var collection = _dbContext.Database.GetCollection<AiSettings>("ai_settings");
            var settings = collection.FindById("default");
            if (settings == null)
            {
                settings = new AiSettings
                {
                    Provider = "OpenAI",
                    ApiKey = "ollama",
                    BaseUrl = "http://localhost:11434/v1",
                    Model = "qwen2.5-coder:7b"
                };
                collection.Insert(settings);
            }
            // Ocultar parcialmente la API key por seguridad al retornarla al cliente
            var safeSettings = new
            {
                settings.Id,
                settings.Provider,
                ApiKey = MaskApiKey(settings.ApiKey),
                HasApiKey = !string.IsNullOrEmpty(settings.ApiKey),
                settings.BaseUrl,
                settings.Model,
                settings.UpdatedAt
            };
            return Ok(safeSettings);
        }

        [AllowAnonymous]
        [HttpPost("settings")]
        public IActionResult SaveSettings([FromBody] AiSettings model)
        {
            var collection = _dbContext.Database.GetCollection<AiSettings>("ai_settings");
            var existing = collection.FindById("default") ?? new AiSettings();

            existing.Provider = model.Provider;
            existing.BaseUrl = model.BaseUrl;
            existing.Model = model.Model;
            existing.UpdatedAt = DateTime.UtcNow;

            // Si el usuario envió una nueva API Key no enmascarada, la actualizamos
            if (!string.IsNullOrEmpty(model.ApiKey) && !model.ApiKey.Contains("****"))
            {
                existing.ApiKey = model.ApiKey;
            }

            collection.Upsert(existing);
            return Ok(new { Message = "Ajustes de IA guardados correctamente.", Settings = existing });
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] IaQueryRequest request)
        {
            if (string.IsNullOrEmpty(request.Prompt) || string.IsNullOrEmpty(request.ProjectRoot))
            {
                return BadRequest("El prompt y el projectRoot son requeridos.");
            }

            try
            {
                var context = await _indexer.IndexProjectAsync(request.ProjectRoot);
                var provider = _providerFactory.GetActiveProvider();
                var answer = await provider.AskAsync(request.Prompt, context);
                return Ok(new { Answer = answer });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR 500 ASK CONTROLLER]: {ex.Message}\n{ex.StackTrace}\n");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("modify-plan")]
        public async Task<IActionResult> GetModifyPlan([FromBody] IaModifyRequest request)
        {
            if (string.IsNullOrEmpty(request.Prompt) || string.IsNullOrEmpty(request.ProjectRoot))
            {
                return BadRequest("El prompt y el projectRoot son requeridos.");
            }

            try
            {
                var context = await _indexer.IndexProjectAsync(request.ProjectRoot);
                var targets = request.TargetFiles ?? new List<string>();
                var provider = _providerFactory.GetActiveProvider();
                var changes = await provider.ModifyProjectAsync(request.Prompt, context, targets);
                return Ok(changes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR 500 IA CONTROLLER]: {ex.Message}\n{ex.StackTrace}\n");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyChanges([FromBody] ApplyChangesRequest request)
        {
            if (string.IsNullOrEmpty(request.ProjectRoot) || request.Changes == null)
            {
                return BadRequest("El projectRoot y la lista de cambios son requeridos.");
            }

            var appliedChanges = new List<string>();
            try
            {
                foreach (var change in request.Changes)
                {
                    var fullPath = Path.Combine(request.ProjectRoot, change.FilePath);
                    
                    if (change.Action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                    {
                        await _fileService.DeleteFileOrDirectoryAsync(fullPath);
                        await _memoryEngine.LearnAsync(request.ProjectRoot, "Eliminado", change.FilePath, $"Archivo eliminado: {change.Description}");
                        appliedChanges.Add($"{change.FilePath} (Eliminado)");
                    }
                    else // Create o Modify
                    {
                        if (System.IO.File.Exists(fullPath))
                        {
                            var backupPath = fullPath + ".bak";
                            await _fileService.CopyFileOrDirectoryAsync(fullPath, backupPath);
                        }

                        await _fileService.WriteFileAsync(fullPath, change.NewContent);
                        var tag = string.IsNullOrEmpty(change.Description) ? "Modificado" : "IA_Edit";
                        await _memoryEngine.LearnAsync(request.ProjectRoot, tag, change.FilePath, change.Description);
                        appliedChanges.Add($"{change.FilePath} ({change.Action})");
                    }
                }

                return Ok(new
                {
                    Message = "Cambios aplicados exitosamente.",
                    Applied = appliedChanges
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al aplicar cambios: {ex.Message}");
            }
        }

        private static string MaskApiKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 8) return "********";
            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        }
    }

    public class IaQueryRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string ProjectRoot { get; set; } = string.Empty;
    }

    public class IaModifyRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string ProjectRoot { get; set; } = string.Empty;
        public List<string>? TargetFiles { get; set; }
    }

    public class ApplyChangesRequest
    {
        public string ProjectRoot { get; set; } = string.Empty;
        public List<FileChange> Changes { get; set; } = new();
    }
}
