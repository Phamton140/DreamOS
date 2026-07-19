using System;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DreamOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StorageController : ControllerBase
    {
        private readonly GoogleDriveStorageProvider _googleDriveProvider;

        public StorageController(GoogleDriveStorageProvider googleDriveProvider)
        {
            _googleDriveProvider = googleDriveProvider;
        }

        [Authorize]
        [HttpGet("google/status")]
        public async Task<IActionResult> GetGoogleStatus()
        {
            try
            {
                var isAuth = await _googleDriveProvider.AuthenticateAsync(_ => { });
                return Ok(new { IsAuthenticated = isAuth });
            }
            catch (Exception ex)
            {
                return Ok(new { IsAuthenticated = false, Error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("google/authorize")]
        public async Task<IActionResult> StartGoogleAuth()
        {
            string authUrl = string.Empty;
            try
            {
                var isAuth = await _googleDriveProvider.AuthenticateAsync(url =>
                {
                    authUrl = url;
                });

                if (isAuth)
                {
                    return Ok(new { IsAuthenticated = true, Message = "Ya autenticado en Google Drive." });
                }

                return Ok(new { IsAuthenticated = false, AuthUrl = authUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al iniciar autorización: {ex.Message}");
            }
        }

        [AllowAnonymous]
        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string? error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return Content("<html><body><h3>Error de autorización de Google:</h3><p>" + error + "</p></body></html>", "text/html");
            }

            if (string.IsNullOrEmpty(code))
            {
                return BadRequest("Código de autorización faltante.");
            }

            try
            {
                await _googleDriveProvider.ProcessCallbackCodeAsync(code);

                var successHtml = @"
                <html>
                <head>
                    <title>Autenticación Exitosa - DreamOS</title>
                    <style>
                        body { font-family: 'Segoe UI', Arial, sans-serif; text-align: center; padding: 50px; background-color: #1e1e2e; color: #cdd6f4; }
                        .card { display: inline-block; padding: 40px; border-radius: 12px; background-color: #313244; box-shadow: 0 4px 10px rgba(0,0,0,0.3); }
                        h2 { color: #a6e3a1; }
                        p { font-size: 1.1em; }
                    </style>
                </head>
                <body>
                    <div class='card'>
                        <h2>¡Conexión Exitosa con Google Drive!</h2>
                        <p>El agente DreamOS Dev se ha vinculado correctamente a tu almacenamiento en la nube.</p>
                        <p>Ya puedes cerrar esta ventana y regresar a tu aplicación móvil.</p>
                    </div>
                </body>
                </html>";

                return Content(successHtml, "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<html><body><h3>Fallo en el intercambio de token:</h3><p>{ex.Message}</p></body></html>", "text/html");
            }
        }
    }
}
