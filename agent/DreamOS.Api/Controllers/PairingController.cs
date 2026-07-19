using System;
using System.Net;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace DreamOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PairingController : ControllerBase
    {
        private readonly SecurityService _securityService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ICloudflareTunnelService _tunnelService;

        public PairingController(
            SecurityService securityService, 
            IDeviceRepository deviceRepository, 
            ICloudflareTunnelService tunnelService)
        {
            _securityService = securityService;
            _deviceRepository = deviceRepository;
            _tunnelService = tunnelService;
        }

        [HttpGet("qr")]
        public IActionResult GetPairingDetails()
        {
            var rawToken = _securityService.GenerateTemporaryPairingCode(out _);
            var lanIp = GetLocalLanIp();

            var payload = new
            {
                LanUrl = $"http://{lanIp}:5000",
                TunnelUrl = _tunnelService.TunnelUrl,
                PairingToken = rawToken
            };

            return Ok(payload);
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmPairing([FromBody] PairingRequest request)
        {
            if (string.IsNullOrEmpty(request.PairingToken) || string.IsNullOrEmpty(request.DeviceName))
            {
                return BadRequest("El token de emparejamiento y el nombre del dispositivo son requeridos.");
            }

            if (!_securityService.ValidatePairingCode(request.PairingToken))
            {
                return Unauthorized("Código de emparejamiento inválido o expirado.");
            }

            var deviceId = Guid.NewGuid().ToString();
            var jwtToken = _securityService.GenerateJwtToken(deviceId, request.DeviceName);

            var device = new Device
            {
                Id = deviceId,
                Name = request.DeviceName,
                Os = request.DeviceOs,
                TokenHash = _securityService.HashToken(jwtToken),
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };

            await _deviceRepository.AddAsync(device);

            return Ok(new
            {
                DeviceId = deviceId,
                Token = jwtToken
            });
        }

        private string GetLocalLanIp()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        var ipStr = ip.ToString();
                        // Evitar loopbacks y direcciones APIPA (169.254.x.x)
                        if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254"))
                        {
                            return ipStr;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return "localhost";
        }
    }

    public class PairingRequest
    {
        public string PairingToken { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceOs { get; set; } = string.Empty;
    }
}
