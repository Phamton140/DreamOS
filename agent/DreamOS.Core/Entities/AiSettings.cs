using System;

namespace DreamOS.Core.Entities
{
    public class AiSettings
    {
        public string Id { get; set; } = "default";
        public string Provider { get; set; } = "Gemini"; // "Gemini", "OpenAI"
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string Model { get; set; } = "gpt-4o-mini";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
