using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DreamOS.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WorkspacesController : ControllerBase
    {
        private readonly IWorkspaceRepository _workspaceRepository;

        public WorkspacesController(IWorkspaceRepository workspaceRepository)
        {
            _workspaceRepository = workspaceRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _workspaceRepository.GetAllAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            try
            {
                var ws = await _workspaceRepository.GetByIdAsync(id);
                if (ws == null) return NotFound("Área de trabajo no encontrada.");
                return Ok(ws);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WorkspaceCreateRequest request)
        {
            if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.ProjectRootPath))
            {
                return BadRequest("El nombre y la ruta de directorio raíz son requeridos.");
            }

            try
            {
                var ws = new Workspace
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = request.Name,
                    ProjectRootPath = request.ProjectRootPath.Replace('\\', '/').TrimEnd('/'),
                    ActiveGitBranch = "main",
                    ActiveIaProvider = "Gemini",
                    LastOpened = DateTime.UtcNow,
                    EnvVariables = request.EnvVariables ?? new Dictionary<string, string>(),
                    Settings = request.Settings ?? new Dictionary<string, string>()
                };

                await _workspaceRepository.AddAsync(ws);
                return CreatedAtAction(nameof(GetById), new { id = ws.Id }, ws);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] WorkspaceUpdateRequest request)
        {
            try
            {
                var ws = await _workspaceRepository.GetByIdAsync(id);
                if (ws == null) return NotFound("Área de trabajo no encontrada.");

                if (!string.IsNullOrEmpty(request.Name)) ws.Name = request.Name;
                if (!string.IsNullOrEmpty(request.ProjectRootPath)) ws.ProjectRootPath = request.ProjectRootPath.Replace('\\', '/').TrimEnd('/');
                if (!string.IsNullOrEmpty(request.ActiveGitBranch)) ws.ActiveGitBranch = request.ActiveGitBranch;
                if (!string.IsNullOrEmpty(request.ActiveIaProvider)) ws.ActiveIaProvider = request.ActiveIaProvider;
                if (request.EnvVariables != null) ws.EnvVariables = request.EnvVariables;
                if (request.Settings != null) ws.Settings = request.Settings;
                ws.LastOpened = DateTime.UtcNow;

                await _workspaceRepository.UpdateAsync(ws);
                return Ok(ws);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var ws = await _workspaceRepository.GetByIdAsync(id);
                if (ws == null) return NotFound("Área de trabajo no encontrada.");

                await _workspaceRepository.DeleteAsync(id);
                return Ok("Área de trabajo eliminada exitosamente.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public class WorkspaceCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ProjectRootPath { get; set; } = string.Empty;
        public Dictionary<string, string>? EnvVariables { get; set; }
        public Dictionary<string, string>? Settings { get; set; }
    }

    public class WorkspaceUpdateRequest
    {
        public string? Name { get; set; }
        public string? ProjectRootPath { get; set; }
        public string? ActiveGitBranch { get; set; }
        public string? ActiveIaProvider { get; set; }
        public Dictionary<string, string>? EnvVariables { get; set; }
        public Dictionary<string, string>? Settings { get; set; }
    }
}
