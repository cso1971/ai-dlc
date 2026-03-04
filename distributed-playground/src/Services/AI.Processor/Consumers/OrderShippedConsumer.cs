using System.Text.Json;
using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderShippedConsumer : IConsumer<OrderShipped>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly IOrderApiClient _orderApiClient;
    private readonly ILogger<OrderShippedConsumer> _logger;

    public OrderShippedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        IOrderApiClient orderApiClient,
        ILogger<OrderShippedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _orderApiClient = orderApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderShipped event for Order {OrderId}, Tracking: {TrackingNumber}", 
            message.OrderId, message.TrackingNumber);

        try
        {
            // Fetch complete order data
            var order = await _orderApiClient.GetOrderAsync(message.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Could not fetch order {OrderId} from API", message.OrderId);
                return;
            }

            // Generate embedding with shipping context
            var shippingText = $"""
                {order.ToTextForEmbedding()}
                
                Shipping Event:
                Shipped At: {message.ShippedAt:yyyy-MM-dd HH:mm}
                Carrier: {message.Carrier}
                Tracking Number: {message.TrackingNumber}
                Estimated Delivery: {message.EstimatedDeliveryDate?.ToString("yyyy-MM-dd") ?? "Unknown"}
                
                Destination: {order.ShippingAddress?.City}, {order.ShippingAddress?.CountryCode}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(shippingText, context.CancellationToken);

            // Build payload
            var payload = BuildOrderPayload(order, "Shipped");
            payload["shippedAt"] = message.ShippedAt.ToString("O");
            payload["trackingNumber"] = message.TrackingNumber ?? "";
            payload["carrier"] = message.Carrier ?? "";
            payload["estimatedDeliveryDate"] = message.EstimatedDeliveryDate?.ToString("O") ?? "";

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            _logger.LogInformation("Order {OrderId} shipping processed. Carrier: {Carrier}, ETA: {ETA}",
                message.OrderId, message.Carrier, message.EstimatedDeliveryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderShipped event for Order {OrderId}", message.OrderId);
            throw;
        }
    }

    private static Dictionary<string, object> BuildOrderPayload(OrderResponse order, string eventType)
    {
        return new Dictionary<string, object>
        {
            ["orderId"] = order.Id.ToString(),
            ["customerId"] = order.CustomerId.ToString(),
            ["customerReference"] = order.CustomerReference ?? "",
            ["status"] = order.Status.ToString(),
            ["eventType"] = eventType,
            ["currencyCode"] = order.CurrencyCode,
            ["grandTotal"] = (double)order.GrandTotal,
            ["priority"] = order.Priority,
            ["lineCount"] = order.Lines.Count,
            ["productCodes"] = string.Join(", ", order.Lines.Select(l => l.ProductCode)),
            ["shippingCity"] = order.ShippingAddress?.City ?? "",
            ["shippingCountry"] = order.ShippingAddress?.CountryCode ?? "",
            ["shippingMethod"] = order.ShippingMethod ?? "",
            ["recipientName"] = order.ShippingAddress?.RecipientName ?? "",
            ["createdAt"] = order.CreatedAt.ToString("O"),
            ["orderText"] = order.ToTextForEmbedding()
        };
    }
}
