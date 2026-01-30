using System.Text.Json;
using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderDeliveredConsumer : IConsumer<OrderDelivered>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly IOrderApiClient _orderApiClient;
    private readonly ILogger<OrderDeliveredConsumer> _logger;

    public OrderDeliveredConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        IOrderApiClient orderApiClient,
        ILogger<OrderDeliveredConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _orderApiClient = orderApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderDelivered> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderDelivered event for Order {OrderId}, ReceivedBy: {ReceivedBy}", 
            message.OrderId, message.ReceivedBy);

        try
        {
            // Fetch complete order data
            var order = await _orderApiClient.GetOrderAsync(message.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Could not fetch order {OrderId} from API", message.OrderId);
                return;
            }

            // Calculate delivery time if possible
            var deliveryDuration = order.ShippedAt.HasValue 
                ? (message.DeliveredAt - order.ShippedAt.Value).TotalDays 
                : (double?)null;

            // Generate embedding with delivery context
            var deliveryText = $"""
                {order.ToTextForEmbedding()}
                
                Delivery Event:
                Delivered At: {message.DeliveredAt:yyyy-MM-dd HH:mm}
                Received By: {message.ReceivedBy ?? "Unknown"}
                Delivery Notes: {message.DeliveryNotes ?? "None"}
                
                Delivery Performance:
                Shipped At: {order.ShippedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown"}
                Transit Time: {(deliveryDuration.HasValue ? $"{deliveryDuration:F1} days" : "Unknown")}
                On-Time: {(order.EstimatedDeliveryDate.HasValue && DateOnly.FromDateTime(message.DeliveredAt) <= order.EstimatedDeliveryDate.Value ? "Yes" : "Unknown")}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(deliveryText, context.CancellationToken);

            // Build payload
            var payload = BuildOrderPayload(order, "Delivered");
            payload["deliveredAt"] = message.DeliveredAt.ToString("O");
            payload["receivedBy"] = message.ReceivedBy ?? "";
            payload["deliveryNotes"] = message.DeliveryNotes ?? "";
            if (deliveryDuration.HasValue)
                payload["transitDays"] = deliveryDuration.Value;

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze delivery performance
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                "Order Delivered",
                deliveryText,
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} delivery processed. Transit: {TransitDays} days. Analysis: {Analysis}", 
                message.OrderId, deliveryDuration?.ToString("F1") ?? "N/A",
                analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderDelivered event for Order {OrderId}", message.OrderId);
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
            ["trackingNumber"] = order.TrackingNumber ?? "",
            ["carrier"] = order.Carrier ?? "",
            ["createdAt"] = order.CreatedAt.ToString("O"),
            ["orderText"] = order.ToTextForEmbedding()
        };
    }
}
