using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;

namespace AI.Processor.Services;

public class OllamaService : IOllamaService
{
    private readonly OllamaApiClient _client;
    private readonly string _model;
    private readonly string _embeddingModel;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(IConfiguration configuration, ILogger<OllamaService> logger)
    {
        _logger = logger;
        var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _model = configuration["Ollama:Model"] ?? "llama3.2";
        _embeddingModel = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        
        _client = new OllamaApiClient(new Uri(baseUrl));
        _logger.LogInformation("OllamaService initialized with endpoint {BaseUrl}, model {Model}, embedding model {EmbeddingModel}", 
            baseUrl, _model, _embeddingModel);
    }

    public async Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Generating completion for prompt: {Prompt}", prompt.Substring(0, Math.Min(100, prompt.Length)));
            
            var request = new GenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = false
            };

            var responseBuilder = new System.Text.StringBuilder();
            await foreach (var response in _client.GenerateAsync(request, cancellationToken))
            {
                if (response?.Response != null)
                {
                    responseBuilder.Append(response.Response);
                }
            }

            var result = responseBuilder.ToString();
            _logger.LogDebug("Generated completion: {Response}", result.Substring(0, Math.Min(100, result.Length)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion");
            throw;
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Generating embedding for text: {Text}", text.Substring(0, Math.Min(100, text.Length)));
            
            var request = new EmbedRequest
            {
                Model = _embeddingModel,
                Input = [text]
            };

            var response = await _client.EmbedAsync(request, cancellationToken);
            var embedding = response?.Embeddings?.FirstOrDefault()?.ToArray() ?? Array.Empty<float>();
            
            _logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            throw;
        }
    }

    public async Task<string> SummarizeOrderAsync(Guid orderId, string orderDetails, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            You are an AI assistant helping to summarize order information.
            Please provide a concise summary of the following order:
            
            Order ID: {orderId}
            {orderDetails}
            
            Summary (2-3 sentences):
            """;

        return await GenerateCompletionAsync(prompt, cancellationToken);
    }

    public async Task<string> AnalyzeOrderEventAsync(string eventType, string eventData, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            You are an AI assistant analyzing order events for a business.
            Please analyze the following order event and provide insights:
            
            Event Type: {eventType}
            Event Data: {eventData}
            
            Analysis (include potential business implications and recommendations):
            """;

        return await GenerateCompletionAsync(prompt, cancellationToken);
    }
}
