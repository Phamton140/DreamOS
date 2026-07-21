using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DreamOS.Core.Entities;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;
using DreamOS.Infrastructure.Data;

namespace DreamOS.Infrastructure.Services
{
    public class OpenAiIaProvider : IIaProvider
    {
        public string ProviderName => "OpenAI";

        private readonly HttpClient _httpClient;
        private readonly LiteDbContext _dbContext;

        public OpenAiIaProvider(LiteDbContext dbContext)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            _dbContext = dbContext;
        }

        private AiSettings GetSettings()
        {
            var collection = _dbContext.Database.GetCollection<AiSettings>("ai_settings");
            return collection.FindById("default") ?? new AiSettings();
        }

        public async Task<string> AskAsync(string prompt, ProjectContext context)
        {
            var settings = GetSettings();
            var apiKey = !string.IsNullOrEmpty(settings.ApiKey) 
                ? settings.ApiKey 
                : Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

            var baseUrl = string.IsNullOrEmpty(settings.BaseUrl) ? "https://api.openai.com/v1" : settings.BaseUrl.TrimEnd('/');
            var isLocal = baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1");

            if (string.IsNullOrEmpty(apiKey) && !isLocal)
            {
                throw new InvalidOperationException("API Key no configurada en Ajustes de IA.");
            }
            if (string.IsNullOrEmpty(apiKey)) apiKey = "ollama";

            var model = string.IsNullOrEmpty(settings.Model) ? "gpt-4o-mini" : settings.Model;
            var url = $"{baseUrl}/chat/completions";

            var contextSummary = BuildContextSummary(context);
            var userPrompt = $"Contexto del Proyecto:\n{contextSummary}\n\nPregunta del usuario:\n{prompt}";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "Eres un asistente conversacional de desarrollo de software en DreamOS Dev. Responde de forma concisa, directa y natural al saludo o consulta del usuario. No hagas resúmenes del proyecto ni listes archivos a menos que el usuario lo solicite explícitamente." },
                    new { role = "user", content = userPrompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                if (errorText.Contains("CreditsError") || errorText.Contains("Insufficient balance"))
                {
                    throw new InvalidOperationException("Saldo insuficiente en tu cuenta de OpenCode/OpenGO. Por favor recarga tus créditos en opencode.ai o selecciona Google Gemini en los Ajustes (🧠).");
                }
                if (errorText.Contains("model") && errorText.Contains("not found"))
                {
                    throw new InvalidOperationException($"El modelo '{model}' no fue encontrado en Ollama. Asegúrate de incluir la etiqueta como '{model}:7b' en los Ajustes de IA (🧠).");
                }
                throw new InvalidOperationException($"Error en servicio de IA ({response.StatusCode}): {errorText}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content ?? "Sin respuesta del modelo.";
        }

        public async Task<List<FileChange>> ModifyProjectAsync(string prompt, ProjectContext context, List<string> targetFiles)
        {
            var settings = GetSettings();
            var apiKey = !string.IsNullOrEmpty(settings.ApiKey) 
                ? settings.ApiKey 
                : Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

            var baseUrl = string.IsNullOrEmpty(settings.BaseUrl) ? "https://api.openai.com/v1" : settings.BaseUrl.TrimEnd('/');
            var isLocal = baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1");

            if (string.IsNullOrEmpty(apiKey) && !isLocal)
            {
                throw new InvalidOperationException("API Key de OpenAI/OpenGO no configurada en Ajustes de IA.");
            }
            if (string.IsNullOrEmpty(apiKey)) apiKey = "ollama";
            var model = string.IsNullOrEmpty(settings.Model) ? "gpt-4o-mini" : settings.Model;
            var url = $"{baseUrl}/chat/completions";

            var contextSummary = BuildContextSummary(context);
            var fileListStr = string.Join(", ", targetFiles);

            var sbFiles = new StringBuilder();
            var filesToLoad = new List<string>(targetFiles);

            if (filesToLoad.Count == 0)
            {
                var relevantExtensions = new[] { ".txt", ".cs", ".dart", ".js", ".ts", ".json", ".html", ".css", ".md" };
                foreach (var fileDesc in context.FileTreeSummary.Where(f => !f.IsDirectory).Take(15))
                {
                    var ext = Path.GetExtension(fileDesc.RelativePath).ToLower();
                    if (relevantExtensions.Contains(ext))
                    {
                        filesToLoad.Add(fileDesc.RelativePath);
                    }
                }
            }

            foreach (var relPath in filesToLoad)
            {
                var fullPath = Path.Combine(context.ProjectRoot, relPath);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var contentText = await File.ReadAllTextAsync(fullPath);
                        sbFiles.AppendLine($"--- CONTENIDO DE: {relPath} ---");
                        sbFiles.AppendLine(contentText);
                        sbFiles.AppendLine($"--- FIN DE: {relPath} ---\n");
                    }
                    catch
                    {
                        // Silenciar fallos de lectura individuales
                    }
                }
            }

            var systemPrompt = @"Eres un agente autónomo de desarrollo en DreamOS Dev.
Analiza la solicitud y los archivos provistos. Debes generar las modificaciones necesarias.
Debes devolver ÚNICAMENTE un objeto JSON estructurado con el siguiente esquema exacto:
{
  ""changes"": [
    {
      ""filePath"": ""ruta/relativa/al/archivo"",
      ""action"": ""Create"" o ""Modify"" o ""Delete"",
      ""newContent"": ""Contenido completo final del archivo. Para Delete puede ir vacío."",
      ""description"": ""Breve explicación del cambio realizado""
    }
  ]
}
Importante: El campo 'newContent' debe contener todo el código fuente listo para reemplazar. Asegúrate de escapar correctamente caracteres especiales para mantener un JSON válido.";

            var userPrompt = $"Contexto del Proyecto:\n{contextSummary}\n\n" +
                              $"Contenido actual de los archivos:\n{sbFiles}\n\n" +
                              $"Archivos objetivos a editar: {fileListStr}\n\n" +
                              $"Instrucción del usuario:\n{prompt}\n\n" +
                              $"Genera el JSON estructurado con la propiedad 'changes'.";

            var requestBody = new
            {
                model = model,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                if (errorText.Contains("CreditsError") || errorText.Contains("Insufficient balance"))
                {
                    throw new InvalidOperationException("Saldo insuficiente en tu cuenta de OpenCode/OpenGO. Por favor recarga tus créditos en opencode.ai o selecciona Google Gemini en los Ajustes (🧠).");
                }
                throw new InvalidOperationException($"Error en API de OpenCode/OpenAI ({response.StatusCode}): {errorText}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            try
            {
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrEmpty(text))
                {
                    return new List<FileChange>();
                }

                // Limpiar bloques Markdown si existieran
                text = text.Trim();
                if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text.Substring(7);
                else if (text.StartsWith("```")) text = text.Substring(3);
                if (text.EndsWith("```")) text = text.Substring(0, text.Length - 3);
                text = text.Trim();

                var firstBrace = text.IndexOf('{');
                var lastBrace = text.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    text = text.Substring(firstBrace, lastBrace - firstBrace + 1);
                }

                text = Regex.Replace(text, @"\""\s*\+\s*\""", "");

                var wrapper = JsonSerializer.Deserialize<GeminiIaProvider.FileChangesWrapper>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return wrapper?.Changes ?? new List<FileChange>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parseando respuesta de OpenAI/OpenGO: {ex.Message}. Respuesta: {responseJson}");
            }
        }

        private string BuildContextSummary(ProjectContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"- Root del Proyecto: {context.ProjectRoot}");
            sb.AppendLine($"- Nombre del Proyecto: {context.ProjectName}");
            sb.AppendLine($"- Tecnologías: {string.Join(", ", context.Technologies)}");
            sb.AppendLine($"- Arquitectura: {context.ArchitectureType}");
            return sb.ToString();
        }
    }
}
