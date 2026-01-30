using System.Text.Json;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderDeliveredConsumer : IConsumer<OrderDelivered>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<OrderDeliveredConsumer> _logger;

    public OrderDeliveredConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ILogger<OrderDeliveredConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderDelivered> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderDelivered event for Order {OrderId}, ReceivedBy: {ReceivedBy}", 
            message.OrderId, message.ReceivedBy);

        try
        {
            // Create delivery details for embedding
            var deliveryDetails = $"""
                Order delivered at {message.DeliveredAt}
                Received by: {message.ReceivedBy}
                Notes: {message.DeliveryNotes}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(deliveryDetails, context.CancellationToken);

            var payload = new Dictionary<string, object>
            {
                ["status"] = "Delivered",
                ["deliveredAt"] = message.DeliveredAt.ToString("O"),
                ["receivedBy"] = message.ReceivedBy ?? "",
                ["deliveryNotes"] = message.DeliveryNotes ?? ""
            };

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze delivery event
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                "OrderDelivered",
                JsonSerializer.Serialize(message),
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} delivery processed. AI Analysis: {Analysis}", 
                message.OrderId, analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderDelivered event for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
