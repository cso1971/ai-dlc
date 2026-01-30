namespace Common.Configuration;

/// <summary>
/// Configuration settings for Ollama LLM integration.
/// </summary>
public class OllamaSettings
{
    public const string SectionName = "Ollama";
    
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "llama3.2";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
