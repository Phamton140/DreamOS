using System;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DreamOS.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TerminalController : ControllerBase
    {
        private readonly ITerminalService _terminalService;

        public TerminalController(ITerminalService terminalService)
        {
            _terminalService = terminalService;
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunCommand([FromBody] CommandRunRequest request)
        {
            if (string.IsNullOrEmpty(request.Command))
            {
                return BadRequest("El comando es requerido.");
            }

            var workingDir = string.IsNullOrEmpty(request.WorkingDirectory) 
                ? @"C:\Proyectos" 
                : request.WorkingDirectory;

            try
            {
                var output = await _terminalService.ExecuteCommandAsync(request.Command, workingDir);
                return Ok(new { Output = output });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al ejecutar comando: {ex.Message}");
            }
        }
    }

    public class CommandRunRequest
    {
        public string Command { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
    }
}
