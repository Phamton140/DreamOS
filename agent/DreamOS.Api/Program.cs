using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using DreamOS.Infrastructure.Services;
using DreamOS.Infrastructure.Repositories;
using DreamOS.Infrastructure.Plugins;
using DreamOS.Api.Services;
using DreamOS.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Escuchar en todas las IPs locales para acceso LAN directo
builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");

// Registrar Servicios Base e Infraestructura
builder.Services.AddSingleton<LiteDbContext>();
builder.Services.AddSingleton<SecurityService>();
builder.Services.AddSingleton<ICloudflareTunnelService, CloudflareTunnelService>();

builder.Services.AddTransient<ITerminalService, TerminalService>();
builder.Services.AddTransient<IFileService, FileService>();
builder.Services.AddTransient<IMemoryEngine, MemoryEngine>();
builder.Services.AddTransient<IProjectIndexer, ProjectIndexer>();

// Registrar Repositorios
builder.Services.AddTransient<IDeviceRepository, LiteDbDeviceRepository>();
builder.Services.AddTransient<IBuildHistoryRepository, LiteDbBuildHistoryRepository>();
builder.Services.AddTransient<IAuditLogRepository, LiteDbAuditLogRepository>();
builder.Services.AddTransient<ITaskScheduleRepository, LiteDbTaskScheduleRepository>();
builder.Services.AddTransient<IWorkspaceRepository, LiteDbWorkspaceRepository>();

// Registrar Plugins de Proyectos
builder.Services.AddTransient<IProjectPlugin, FlutterPlugin>();
builder.Services.AddTransient<IProjectPlugin, LaravelPlugin>();
builder.Services.AddTransient<PluginService>();

// Registrar IA y Almacenamiento
builder.Services.AddTransient<GeminiIaProvider>();
builder.Services.AddTransient<OpenAiIaProvider>();
builder.Services.AddTransient<OllamaIaProvider>();
builder.Services.AddTransient<IaProviderFactory>();
builder.Services.AddTransient<IIaProvider>(sp => sp.GetRequiredService<IaProviderFactory>().GetActiveProvider());

builder.Services.AddTransient<GoogleDriveStorageProvider>();
builder.Services.AddTransient<IStorageProvider, StorageService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

// Configurar Autenticación JWT de forma diferida resolviendo SecurityService desde DI
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SecurityService>((options, securityService) =>
    {
        options.TokenValidationParameters = securityService.GetValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/console"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Agregar CORS para permitir llamadas de la app cliente
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Registrar Servicio de Túnel de Cloudflare en segundo plano
builder.Services.AddHostedService<TunnelHostedService>();

var app = builder.Build();

// Configuración HTTP Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ConsoleHub>("/hubs/console");

// Endpoint temporal para validar status de conexión y túnel
app.MapGet("/", () => "DreamOS Dev Agent Online\nVersion: 0.1\nStatus: OK");

app.MapGet("/qr-page", () => Results.Content(@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>DreamOS Dev — Emparejamiento QR</title>
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js""></script>
    <style>
        * { box-sizing: border-box; }
        body { background-color: #0B0B14; color: #FFFFFF; font-family: 'Segoe UI', system-ui, sans-serif; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100vh; margin: 0; padding: 20px; }
        .card { background: #151528; padding: 32px; border-radius: 24px; box-shadow: 0 16px 48px rgba(0, 206, 201, 0.12); text-align: center; border: 1px solid #282846; max-width: 500px; width: 100%; }
        .badge { display: inline-flex; align-items: center; gap: 8px; background: rgba(0, 206, 201, 0.12); color: #00CEC9; font-size: 12px; font-weight: 600; padding: 6px 14px; border-radius: 20px; margin-bottom: 16px; border: 1px solid rgba(0, 206, 201, 0.3); }
        .dot { width: 8px; height: 8px; background-color: #00CEC9; border-radius: 50%; box-shadow: 0 0 10px #00CEC9; }
        .logo { font-size: 28px; font-weight: bold; color: #FFFFFF; margin-bottom: 6px; display: flex; align-items: center; justify-content: center; gap: 8px; }
        .subtitle { color: #A0A0C0; font-size: 13px; margin-bottom: 24px; line-height: 1.5; }
        .qr-wrapper { background: #FFFFFF; padding: 20px; border-radius: 18px; display: inline-block; margin-bottom: 20px; box-shadow: 0 8px 24px rgba(0,0,0,0.3); }
        .pin-box { background: rgba(0, 206, 201, 0.08); border: 1px solid rgba(0, 206, 201, 0.3); padding: 14px; border-radius: 12px; margin-bottom: 16px; width: 100%; word-break: break-all; }
        .pin-label { font-size: 11px; color: #A0A0C0; text-transform: uppercase; letter-spacing: 1px; font-weight: 600; margin-bottom: 6px; }
        .pin-value { font-size: 15px; font-weight: bold; color: #00CEC9; font-family: monospace; word-break: break-all; letter-spacing: 0.5px; }
        .btn-group { display: flex; gap: 10px; margin-bottom: 20px; }
        .btn { flex: 1; padding: 12px; border-radius: 10px; font-size: 13px; font-weight: 600; cursor: pointer; border: none; transition: all 0.2s ease; display: flex; align-items: center; justify-content: center; gap: 6px; }
        .btn-primary { background: #00CEC9; color: #0F0F1E; }
        .btn-primary:hover { background: #55EFC4; transform: translateY(-1px); }
        .btn-secondary { background: #22223B; color: #FFFFFF; border: 1px solid #363659; }
        .btn-secondary:hover { background: #2E2E4E; }
        .payload-preview { background: #0A0A14; padding: 14px; border-radius: 10px; font-family: monospace; font-size: 11px; color: #55EFC4; text-align: left; white-space: pre-wrap; word-break: break-all; border: 1px solid #22223D; max-height: 140px; overflow-y: auto; }
        #toast { visibility: hidden; min-width: 200px; background-color: #00CEC9; color: #000; text-align: center; border-radius: 8px; padding: 10px; position: fixed; z-index: 1; bottom: 30px; font-weight: bold; font-size: 13px; }
        #toast.show { visibility: visible; animation: fadein 0.5s, fadeout 0.5s 2.5s; }
        @keyframes fadein { from {bottom: 0; opacity: 0;} to {bottom: 30px; opacity: 1;} }
        @keyframes fadeout { from {bottom: 30px; opacity: 1;} to {bottom: 0; opacity: 0;} }
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""badge""><div class=""dot""></div> AGENTE ACTIVO</div>
        <div class=""logo"">🚀 DreamOS Dev</div>
        <div class=""subtitle"">Escanea este código QR desde la app móvil para vincular tu PC instantáneamente sin copiar códigos.</div>
        
        <div class=""qr-wrapper"">
            <div id=""qrcode""></div>
        </div>

        <div class=""pin-box"">
            <div class=""pin-label"">Token de Emparejamiento Rápido</div>
            <div class=""pin-value"" id=""token-val"">Cargando...</div>
        </div>

        <div class=""btn-group"">
            <button class=""btn btn-primary"" onclick=""loadQR()"">🔄 Generar Nuevo Token</button>
            <button class=""btn btn-secondary"" onclick=""copyJson()"">📋 Copiar JSON</button>
        </div>

        <div class=""payload-preview"" id=""raw-json"">Cargando datos...</div>
    </div>
    <div id=""toast"">¡JSON copiado al portapapeles!</div>

    <script>
        let currentJson = '';

        function loadQR() {
            document.getElementById('token-val').innerText = 'Generando...';
            document.getElementById('raw-json').innerText = 'Obteniendo nuevo token...';
            document.getElementById('qrcode').innerHTML = '';

            fetch('/api/pairing/qr')
                .then(res => res.json())
                .then(data => {
                    currentJson = JSON.stringify(data, null, 2);
                    document.getElementById('raw-json').innerText = currentJson;
                    const token = data.pairingToken || data.PairingToken;
                    document.getElementById('token-val').innerText = token;

                    const qrData = JSON.stringify(data);
                    new QRCode(document.getElementById(""qrcode""), {
                        text: qrData,
                        width: 220,
                        height: 220,
                        colorDark : ""#151528"",
                        colorLight : ""#ffffff"",
                        correctLevel : QRCode.CorrectLevel.M
                    });
                })
                .catch(err => {
                    document.getElementById('raw-json').innerText = ""Error: "" + err;
                });
        }

        function copyJson() {
            if (!currentJson) return;
            navigator.clipboard.writeText(currentJson).then(() => {
                const toast = document.getElementById(""toast"");
                toast.className = ""show"";
                setTimeout(() => { toast.className = toast.className.replace(""show"", """"); }, 3000);
            });
        }

        window.onload = loadQR;
    </script>
</body>
</html>", "text/html"));

app.Run();
