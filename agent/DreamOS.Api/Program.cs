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
builder.Services.AddTransient<OllamaIaProvider>();
builder.Services.AddTransient<IIaProvider, IaService>();

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

app.Run();
