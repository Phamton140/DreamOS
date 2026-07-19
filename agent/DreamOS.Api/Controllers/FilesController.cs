using System;
using System.Collections.Generic;
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
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpGet("explore")]
        public IActionResult Explore([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    // Si no se especifica ruta, retornar raíces de discos lógicos (C:\, D:\, etc.)
                    var roots = new List<FileDescriptor>();
                    foreach (var drive in System.IO.DriveInfo.GetDrives())
                    {
                        if (drive.IsReady)
                        {
                            roots.Add(new FileDescriptor
                            {
                                RelativePath = drive.Name.Replace('\\', '/'),
                                IsDirectory = true,
                                FileType = "Drive",
                                Size = drive.TotalSize
                            });
                        }
                    }
                    return Ok(roots);
                }

                var files = _fileService.ListDirectory(path);
                return Ok(files);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al listar directorio: {ex.Message}");
            }
        }

        [HttpGet("read")]
        public async Task<IActionResult> Read([FromQuery] string path)
        {
            try
            {
                var content = await _fileService.ReadFileAsync(path);
                return Ok(new { Content = content });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (System.IO.FileNotFoundException)
            {
                return NotFound("Archivo no encontrado.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("write")]
        public async Task<IActionResult> Write([FromBody] FileWriteRequest request)
        {
            try
            {
                await _fileService.WriteFileAsync(request.Path, request.Content);
                return Ok("Archivo guardado exitosamente.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("create-directory")]
        public async Task<IActionResult> CreateDirectory([FromBody] FileActionRequest request)
        {
            try
            {
                await _fileService.CreateDirectoryAsync(request.Path);
                return Ok("Directorio creado exitosamente.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] FileActionRequest request)
        {
            try
            {
                await _fileService.DeleteFileOrDirectoryAsync(request.Path);
                return Ok("Archivo o directorio eliminado exitosamente.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("move")]
        public async Task<IActionResult> Move([FromBody] FileMoveRequest request)
        {
            try
            {
                await _fileService.MoveFileOrDirectoryAsync(request.Source, request.Destination);
                return Ok("Movido exitosamente.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("copy")]
        public async Task<IActionResult> Copy([FromBody] FileMoveRequest request)
        {
            try
            {
                await _fileService.CopyFileOrDirectoryAsync(request.Source, request.Destination);
                return Ok("Copiado exitosamente.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public class FileWriteRequest
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class FileActionRequest
    {
        public string Path { get; set; } = string.Empty;
    }

    public class FileMoveRequest
    {
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
    }
}
