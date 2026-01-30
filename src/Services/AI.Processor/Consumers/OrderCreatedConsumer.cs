using System.Text.Json;
using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly IOrderApiClient _orderApiClient;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        IOrderApiClient orderApiClient,
        ILogger<OrderCreatedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _orderApiClient = orderApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCreated event for Order {OrderId}", message.OrderId);

        try
        {
            // Fetch complete order data from Ordering API
            var order = await _orderApiClient.GetOrderAsync(message.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Could not fetch order {OrderId} from API, using event data only", message.OrderId);
                return;
            }

            // Generate embedding using the complete order text
            var orderText = order.ToTextForEmbedding();
            var embedding = await _ollamaService.GenerateEmbeddingAsync(orderText, context.CancellationToken);

            // Build rich payload with complete order information
            var payload = BuildOrderPayload(order, "Created");

            // Store in Qdrant
            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze with LLM
            var analysisPrompt = $"""
                Analyze this new order and provide business insights:
                
                {orderText}
                
                Consider: customer value, product mix, shipping complexity, priority handling.
                """;
            
            var analysis = await _ollamaService.GenerateCompletionAsync(analysisPrompt, context.CancellationToken);

            _logger.LogInformation("Order {OrderId} processed and stored in RAG. Total: {GrandTotal} {Currency}. Analysis: {Analysis}", 
                message.OrderId, order.GrandTotal, order.CurrencyCode, 
                analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event for Order {OrderId}", message.OrderId);
            throw;
        }
    }

    private static Dictionary<string, object> BuildOrderPayload(OrderResponse order, string eventType)
    {
        return new Dictionary<string, object>
        {
            // Identifiers
            ["orderId"] = order.Id.ToString(),
            ["customerId"] = order.CustomerId.ToString(),
            ["customerReference"] = order.CustomerReference ?? "",
            
            // Status & Event
            ["status"] = order.Status.ToString(),
            ["eventType"] = eventType,
            
            // Financial
            ["currencyCode"] = order.CurrencyCode,
            ["subtotal"] = (double)order.Subtotal,
            ["totalTax"] = (double)order.TotalTax,
            ["grandTotal"] = (double)order.GrandTotal,
            
            // Order details
            ["priority"] = order.Priority,
            ["lineCount"] = order.Lines.Count,
            ["productCodes"] = string.Join(", ", order.Lines.Select(l => l.ProductCode)),
            
            // Shipping
            ["shippingCity"] = order.ShippingAddress?.City ?? "",
            ["shippingCountry"] = order.ShippingAddress?.CountryCode ?? "",
            ["shippingMethod"] = order.ShippingMethod ?? "",
            ["recipientName"] = order.ShippingAddress?.RecipientName ?? "",
            
            // Dates
            ["createdAt"] = order.CreatedAt.ToString("O"),
            ["requestedDeliveryDate"] = order.RequestedDeliveryDate?.ToString("O") ?? "",
            
            // Tracking
            ["trackingNumber"] = order.TrackingNumber ?? "",
            ["carrier"] = order.Carrier ?? "",
            
            // Full text for search
            ["orderText"] = order.ToTextForEmbedding()
        };
    }
}
