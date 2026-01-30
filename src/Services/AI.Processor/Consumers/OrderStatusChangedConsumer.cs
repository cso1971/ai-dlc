using System.Text.Json;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderStatusChangedConsumer : IConsumer<OrderStatusChanged>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<OrderStatusChangedConsumer> _logger;

    public OrderStatusChangedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ILogger<OrderStatusChangedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderStatusChanged event for Order {OrderId}: {PreviousStatus} -> {NewStatus}", 
            message.OrderId, message.PreviousStatus, message.NewStatus);

        try
        {
            // Update status in Qdrant
            var statusText = $"Order status changed from {message.PreviousStatus} to {message.NewStatus}";
            var embedding = await _ollamaService.GenerateEmbeddingAsync(statusText, context.CancellationToken);

            var payload = new Dictionary<string, object>
            {
                ["status"] = message.NewStatus.ToString(),
                ["previousStatus"] = message.PreviousStatus.ToString(),
                ["changedAt"] = message.ChangedAt.ToString("O")
            };

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze the status change
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                "OrderStatusChanged",
                JsonSerializer.Serialize(message),
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} status change processed. AI Analysis: {Analysis}", 
                message.OrderId, analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderStatusChanged event for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
