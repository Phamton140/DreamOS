using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Core.Models;
using DreamOS.Infrastructure.Data;
using LiteDB;


namespace DreamOS.Infrastructure.Services
{
    public class GeminiIaProvider : IIaProvider
    {
        private readonly HttpClient _httpClient;
        private readonly LiteDbContext _dbContext;
        public string ProviderName => "Gemini";

        public GeminiIaProvider(LiteDbContext dbContext)
        {
            _httpClient = new HttpClient();
            _dbContext = dbContext;
        }

        private string GetApiKey()
        {
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                return apiKey;
            }

            // Buscar en base de datos
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "gemini_api_key"));
            if (doc != null && doc.TryGetValue("value", out var val))
            {
                return val.AsString;
            }

            return string.Empty;
        }

        private string GetModelName()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "gemini_model"));
            if (doc != null && doc.TryGetValue("value", out var val))
            {
                return val.AsString;
            }
            return "gemini-flash-latest"; // Default
        }

        public async Task<string> AskAsync(string prompt, ProjectContext context)
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: Gemini API Key no configurada. Configúrala en la app móvil o define GEMINI_API_KEY como variable de entorno.";
            }

            var model = GetModelName();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var contextSummary = BuildContextSummary(context);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"Contexto del Proyecto:\n{contextSummary}\n\nInstrucción/Pregunta del usuario:\n{prompt}" }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = "Eres un asistente de desarrollo IA de élite llamado DreamOS. Ayudas al usuario a entender y mantener su proyecto de software." }
                    }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                return $"Error de API Gemini ({response.StatusCode}): {errorText}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            
            try
            {
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "Sin respuesta.";
            }
            catch (Exception)
            {
                return $"Error al procesar la respuesta de la IA. Crudo: {responseJson}";
            }
        }

        public async Task<List<FileChange>> ModifyProjectAsync(string prompt, ProjectContext context, List<string> targetFiles)
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API Key de Gemini no configurada.");
            }

            var model = GetModelName();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var contextSummary = BuildContextSummary(context);
            var fileListStr = string.Join(", ", targetFiles);

            // Cargar los contenidos reales de los archivos objetivos en disco (o todos los relevantes si no se especifican)
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
                if (System.IO.File.Exists(fullPath))
                {
                    try
                    {
                        var contentText = await System.IO.File.ReadAllTextAsync(fullPath);
                        sbFiles.AppendLine($"--- CONTENIDO DE: {relPath} ---");
                        sbFiles.AppendLine(contentText);
                        sbFiles.AppendLine($"--- FIN DE: {relPath} ---\n");
                    }
                    catch (Exception)
                    {
                        // Silenciar fallos de lectura individuales
                    }
                }
            }

            var systemPrompt = @"Eres un agente autónomo de desarrollo en DreamOS Dev.
Analiza la solicitud y los archivos provistos. Debes generar las modificaciones necesarias.
Debes devolver ÚNICAMENTE un objeto JSON con el siguiente esquema:
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
No agregues explicaciones fuera del JSON.";

            var userPrompt = $"Contexto del Proyecto:\n{contextSummary}\n\n" +
                              $"Contenido actual de los archivos:\n{sbFiles}\n\n" +
                              $"Archivos objetivos a editar: {fileListStr}\n\n" +
                              $"Instrucción del usuario:\n{prompt}\n\n" +
                              $"Genera el JSON con los cambios requeridos. Recuerda retornar el código COMPLETO en 'newContent' para archivos que crees o modifiques.";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = userPrompt }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Error: {response.StatusCode} - {errorText}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            try
            {
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrEmpty(text))
                {
                    return new List<FileChange>();
                }

                // Deserializar lista de cambios
                var wrapper = System.Text.Json.JsonSerializer.Deserialize<FileChangesWrapper>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return wrapper?.Changes ?? new List<FileChange>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parseando respuesta estructurada de la IA: {ex.Message}. Crudo: {responseJson}");
            }
        }

        private string BuildContextSummary(ProjectContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"- Root del Proyecto: {context.ProjectRoot}");
            sb.AppendLine($"- Nombre del Proyecto: {context.ProjectName}");
            sb.AppendLine($"- Tecnologías: {string.Join(", ", context.Technologies)}");
            sb.AppendLine($"- Arquitectura: {context.ArchitectureType}");
            
            if (!string.IsNullOrEmpty(context.LastBuildStatus))
            {
                sb.AppendLine($"- Estado de última compilación: {context.LastBuildStatus}");
            }

            if (!string.IsNullOrEmpty(context.MemoryNotes))
            {
                sb.AppendLine($"- Notas de Memoria del Proyecto:\n{context.MemoryNotes}");
            }

            sb.AppendLine("- Estructura de archivos relevante:");
            foreach (var file in context.FileTreeSummary)
            {
                sb.AppendLine($"  {(file.IsDirectory ? "[DIR]" : "[FILE]")} {file.RelativePath} ({file.FileType})");
            }

            return sb.ToString();
        }

        private class FileChangesWrapper
        {
            [JsonPropertyName("changes")]
            public List<FileChange> Changes { get; set; } = new();
        }
    }
}
