using System.Text.Json;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ILogger<OrderCreatedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCreated event for Order {OrderId}", message.OrderId);

        try
        {
            // Create order details for embedding
            var orderDetails = $"""
                Customer: {message.CustomerId}
                Currency: {message.CurrencyCode}
                Items: {message.Lines?.Count ?? 0}
                Total Value: {message.Lines?.Sum(l => l.Quantity * l.UnitPrice) ?? 0:F2}
                Shipping: {message.ShippingAddress?.City}, {message.ShippingAddress?.CountryCode}
                """;

            // Generate embedding for the order
            var embedding = await _ollamaService.GenerateEmbeddingAsync(orderDetails, context.CancellationToken);

            // Store in Qdrant
            var payload = new Dictionary<string, object>
            {
                ["customerId"] = message.CustomerId.ToString(),
                ["status"] = "Created",
                ["currencyCode"] = message.CurrencyCode ?? "USD",
                ["itemCount"] = message.Lines?.Count ?? 0,
                ["createdAt"] = message.CreatedAt.ToString("O"),
                ["city"] = message.ShippingAddress?.City ?? "",
                ["country"] = message.ShippingAddress?.CountryCode ?? ""
            };

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze with LLM
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                "OrderCreated",
                JsonSerializer.Serialize(message),
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} processed and stored. AI Analysis: {Analysis}", 
                message.OrderId, analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
