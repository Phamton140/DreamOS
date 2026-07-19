using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;

namespace DreamOS.Infrastructure.Services
{
    public class CloudflareTunnelService : ICloudflareTunnelService
    {
        private readonly string _binaryPath;
        private Process? _tunnelProcess;
        private readonly HttpClient _httpClient;

        public string TunnelUrl { get; private set; } = string.Empty;
        public bool IsRunning { get; private set; } = false;

        public CloudflareTunnelService()
        {
            var appDataDir = @"C:\Users\Admin\.gemini\antigravity";
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            _binaryPath = Path.Combine(appDataDir, "cloudflared.exe");
            _httpClient = new HttpClient();
        }

        private async Task EnsureBinaryExistsAsync()
        {
            if (File.Exists(_binaryPath))
            {
                return;
            }

            Console.WriteLine("cloudflared.exe no encontrado. Descargando de Cloudflare...");
            
            // Descarga de la última versión de cloudflared para Windows x64
            var downloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
            
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(_binaryPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
            
            Console.WriteLine("cloudflared.exe descargado exitosamente.");
        }

        public async Task<string> StartTunnelAsync(int localPort)
        {
            if (IsRunning)
            {
                return TunnelUrl;
            }

            await EnsureBinaryExistsAsync();

            TunnelUrl = string.Empty;
            var tcs = new TaskCompletionSource<string>();

            var startInfo = new ProcessStartInfo
            {
                FileName = _binaryPath,
                Arguments = $"tunnel --url http://localhost:{localPort}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _tunnelProcess = new Process { StartInfo = startInfo };

            // Cloudflared escribe sus logs de túnel rápido a Stderr
            _tunnelProcess.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                Console.WriteLine($"[Cloudflare] {e.Data}");

                // Buscar la URL en los logs
                var match = Regex.Match(e.Data, @"https://[a-zA-Z0-9-]+\.trycloudflare\.com");
                if (match.Success)
                {
                    TunnelUrl = match.Value;
                    IsRunning = true;
                    tcs.TrySetResult(TunnelUrl);
                }
            };

            _tunnelProcess.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                
                var match = Regex.Match(e.Data, @"https://[a-zA-Z0-9-]+\.trycloudflare\.com");
                if (match.Success)
                {
                    TunnelUrl = match.Value;
                    IsRunning = true;
                    tcs.TrySetResult(TunnelUrl);
                }
            };

            _tunnelProcess.Start();
            _tunnelProcess.BeginOutputReadLine();
            _tunnelProcess.BeginErrorReadLine();

            // Esperar un tiempo límite de 25 segundos para levantar el túnel
            var timeoutTask = Task.Delay(25000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout
                StopTunnel();
                throw new TimeoutException("Tiempo de espera agotado al iniciar el túnel de Cloudflare.");
            }

            return TunnelUrl;
        }

        public Task StopTunnelAsync()
        {
            StopTunnel();
            return Task.CompletedTask;
        }

        private void StopTunnel()
        {
            try
            {
                if (_tunnelProcess != null && !_tunnelProcess.HasExited)
                {
                    _tunnelProcess.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al detener el túnel de Cloudflare: {ex.Message}");
            }
            finally
            {
                _tunnelProcess = null;
                IsRunning = false;
                TunnelUrl = string.Empty;
            }
        }
    }
}
