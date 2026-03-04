namespace AI.Processor.Services;

public interface IOllamaService
{
    Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken = default);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<string> SummarizeOrderAsync(Guid orderId, string orderDetails, CancellationToken cancellationToken = default);
    Task<string> AnalyzeOrderEventAsync(string eventType, string eventData, CancellationToken cancellationToken = default);
}
