using System.Text.Json;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderCancelledConsumer : IConsumer<OrderCancelled>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<OrderCancelledConsumer> _logger;

    public OrderCancelledConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ILogger<OrderCancelledConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCancelled event for Order {OrderId}, Reason: {Reason}", 
            message.OrderId, message.CancellationReason);

        try
        {
            // Create cancellation details for embedding
            var cancellationDetails = $"""
                Order cancelled at {message.CancelledAt}
                Reason: {message.CancellationReason}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(cancellationDetails, context.CancellationToken);

            var payload = new Dictionary<string, object>
            {
                ["status"] = "Cancelled",
                ["cancelledAt"] = message.CancelledAt.ToString("O"),
                ["cancellationReason"] = message.CancellationReason ?? ""
            };

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze cancellation - this is important for business insights
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                "OrderCancelled",
                JsonSerializer.Serialize(message),
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} cancellation processed. AI Analysis: {Analysis}", 
                message.OrderId, analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCancelled event for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
