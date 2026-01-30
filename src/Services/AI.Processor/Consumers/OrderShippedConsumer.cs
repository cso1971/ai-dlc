using System.Text.Json;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderShippedConsumer : IConsumer<OrderShipped>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<OrderShippedConsumer> _logger;

    public OrderShippedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ILogger<OrderShippedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderShipped event for Order {OrderId}, Tracking: {TrackingNumber}", 
            message.OrderId, message.TrackingNumber);

        try
        {
            // Create shipping details for embedding
            var shippingDetails = $"""
                Order shipped via {message.Carrier}
                Tracking: {message.TrackingNumber}
                Estimated Delivery: {message.EstimatedDeliveryDate}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(shippingDetails, context.CancellationToken);

            var payload = new Dictionary<string, object>
            {
                ["status"] = "Shipped",
                ["trackingNumber"] = message.TrackingNumber ?? "",
                ["carrier"] = message.Carrier ?? "",
                ["shippedAt"] = message.ShippedAt.ToString("O"),
                ["estimatedDeliveryDate"] = message.EstimatedDeliveryDate?.ToString("O") ?? ""
            };

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze shipping event
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                "OrderShipped",
                JsonSerializer.Serialize(message),
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} shipping processed. AI Analysis: {Analysis}", 
                message.OrderId, analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderShipped event for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
