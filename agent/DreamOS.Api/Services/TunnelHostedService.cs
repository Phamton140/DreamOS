using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DreamOS.Api.Services
{
    public class TunnelHostedService : IHostedService
    {
        private readonly ICloudflareTunnelService _tunnelService;
        private readonly SecurityService _securityService;
        private readonly ILogger<TunnelHostedService> _logger;

        public TunnelHostedService(
            ICloudflareTunnelService tunnelService, 
            SecurityService securityService,
            ILogger<TunnelHostedService> logger)
        {
            _tunnelService = tunnelService;
            _securityService = securityService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Iniciando Túnel de Cloudflare en segundo plano...");
            try
            {
                // Kestrel levanta típicamente en 5000 (HTTP) y 5001 (HTTPS).
                // Apuntamos al puerto HTTP local (5000) para simplificar la encriptación local.
                var url = await _tunnelService.StartTunnelAsync(5000);
                _logger.LogInformation($"[DreamOS Tunnel] Túnel activo en: {url}");

                // Generar e imprimir payload inicial para vinculación manual del desarrollador
                var rawToken = _securityService.GenerateTemporaryPairingCode(out _);
                var lanIp = GetLocalLanIp();

                Console.WriteLine("\n=======================================================");
                Console.WriteLine("DREAMOS DEV - PAYLOAD DE EMPAREJAMIENTO QR:");
                Console.WriteLine("=======================================================");
                Console.WriteLine("{");
                Console.WriteLine($"  \"LanUrl\": \"http://{lanIp}:5000\",");
                Console.WriteLine($"  \"TunnelUrl\": \"{url}\",");
                Console.WriteLine($"  \"PairingToken\": \"{rawToken}\"");
                Console.WriteLine("}");
                Console.WriteLine("=======================================================\n");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Fallo al arrancar Cloudflare Tunnel: {ex.Message}");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo Túnel de Cloudflare...");
            await _tunnelService.StopTunnelAsync();
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
}
