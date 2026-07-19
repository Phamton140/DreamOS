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
    public class OllamaIaProvider : IIaProvider
    {
        private readonly HttpClient _httpClient;
        private readonly LiteDbContext _dbContext;
        public string ProviderName => "Ollama";

        public OllamaIaProvider(LiteDbContext dbContext)
        {
            _httpClient = new HttpClient();
            _dbContext = dbContext;
        }

        private string GetOllamaUrl()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "ollama_url"));
            if (doc != null && doc.TryGetValue("value", out var val))
            {
                return val.AsString;
            }
            return "http://localhost:11434"; // Default
        }

        private string GetModelName()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "ollama_model"));
            if (doc != null && doc.TryGetValue("value", out var val))
            {
                return val.AsString;
            }
            return "qwen2.5-coder"; // Default recomendado para desarrollo local
        }

        public async Task<string> AskAsync(string prompt, ProjectContext context)
        {
            var baseUrl = GetOllamaUrl();
            var url = $"{baseUrl.TrimEnd('/')}/api/generate";
            var model = GetModelName();

            var contextSummary = BuildContextSummary(context);

            var requestBody = new
            {
                model = model,
                system = "Eres un asistente de desarrollo IA de élite llamado DreamOS. Ayudas al usuario a entender y mantener su proyecto de software.",
                prompt = $"Contexto del Proyecto:\n{contextSummary}\n\nInstrucción/Pregunta del usuario:\n{prompt}",
                stream = false
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                
                return doc.RootElement.GetProperty("response").GetString() ?? "Sin respuesta.";
            }
            catch (Exception ex)
            {
                return $"Error de conexión con Ollama local ({url}): {ex.Message}. Asegúrate de tener Ollama ejecutándose y el modelo '{model}' descargado.";
            }
        }

        public async Task<List<FileChange>> ModifyProjectAsync(string prompt, ProjectContext context, List<string> targetFiles)
        {
            var baseUrl = GetOllamaUrl();
            var url = $"{baseUrl.TrimEnd('/')}/api/generate";
            var model = GetModelName();

            var contextSummary = BuildContextSummary(context);
            var fileListStr = string.Join(", ", targetFiles);

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
                              $"Archivos objetivos a editar: {fileListStr}\n\n" +
                              $"Instrucción del usuario:\n{prompt}\n\n" +
                              $"Genera el JSON con los cambios requeridos. Recuerda retornar el código COMPLETO en 'newContent' para archivos que crees o modifiques.";

            var requestBody = new
            {
                model = model,
                system = systemPrompt,
                prompt = userPrompt,
                stream = false,
                format = "json" // Obliga a Ollama a retornar formato JSON estructurado
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var text = doc.RootElement.GetProperty("response").GetString();

            if (string.IsNullOrEmpty(text))
            {
                return new List<FileChange>();
            }

            try
            {
                var wrapper = System.Text.Json.JsonSerializer.Deserialize<FileChangesWrapper>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return wrapper?.Changes ?? new List<FileChange>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parseando respuesta estructurada de Ollama local: {ex.Message}. Crudo: {text}");
            }
        }

        private string BuildContextSummary(ProjectContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"- Root: {context.ProjectRoot}");
            sb.AppendLine($"- Proyecto: {context.ProjectName}");
            sb.AppendLine($"- Tecnologías: {string.Join(", ", context.Technologies)}");
            sb.AppendLine($"- Arquitectura: {context.ArchitectureType}");
            
            if (!string.IsNullOrEmpty(context.MemoryNotes))
            {
                sb.AppendLine($"- Notas de Memoria:\n{context.MemoryNotes}");
            }

            sb.AppendLine("- Estructura de archivos relevante:");
            foreach (var file in context.FileTreeSummary)
            {
                sb.AppendLine($"  {(file.IsDirectory ? "[DIR]" : "[FILE]")} {file.RelativePath}");
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
