using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DreamOS.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class IaController : ControllerBase
    {
        private readonly IIaProvider _iaProvider;
        private readonly IProjectIndexer _indexer;
        private readonly IFileService _fileService;
        private readonly IMemoryEngine _memoryEngine;

        public IaController(
            IIaProvider iaProvider,
            IProjectIndexer indexer,
            IFileService fileService,
            IMemoryEngine memoryEngine)
        {
            _iaProvider = iaProvider;
            _indexer = indexer;
            _fileService = fileService;
            _memoryEngine = memoryEngine;
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
                var answer = await _iaProvider.AskAsync(request.Prompt, context);
                return Ok(new { Answer = answer });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error en IA: {ex.Message}");
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
                
                // Si la IA necesita inferir los archivos, pasamos una lista vacía de objetivos
                var targets = request.TargetFiles ?? new List<string>();
                
                // Si hay archivos en la memoria relacionados, la IA los recibirá en el contexto
                var changes = await _iaProvider.ModifyProjectAsync(request.Prompt, context, targets);
                return Ok(changes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR 500 IA CONTROLLER]: {ex.Message}\n{ex.StackTrace}\n");
                return StatusCode(500, $"Error al planificar cambios: {ex.Message}");
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
                        // Copia de seguridad antes de modificar
                        if (System.IO.File.Exists(fullPath))
                        {
                            var backupPath = fullPath + ".bak";
                            await _fileService.CopyFileOrDirectoryAsync(fullPath, backupPath);
                        }

                        await _fileService.WriteFileAsync(fullPath, change.NewContent);
                        
                        // Grabar en memoria el propósito de este archivo modificado
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
