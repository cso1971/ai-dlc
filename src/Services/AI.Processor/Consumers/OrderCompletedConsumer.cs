using System.Text.Json;
using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderCompletedConsumer : IConsumer<OrderCompleted>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly IOrderApiClient _orderApiClient;
    private readonly ILogger<OrderCompletedConsumer> _logger;

    public OrderCompletedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        IOrderApiClient orderApiClient,
        ILogger<OrderCompletedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _orderApiClient = orderApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCompleted> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCompleted event for Order {OrderId}, Invoice: {InvoiceId}", 
            message.OrderId, message.InvoiceId);

        try
        {
            // Fetch complete order data
            var order = await _orderApiClient.GetOrderAsync(message.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Could not fetch order {OrderId} from API", message.OrderId);
                return;
            }

            // Calculate order lifecycle metrics
            var totalCycleTime = (message.CompletedAt - order.CreatedAt).TotalDays;
            var deliveryTime = order.ShippedAt.HasValue && order.DeliveredAt.HasValue
                ? (order.DeliveredAt.Value - order.ShippedAt.Value).TotalDays
                : (double?)null;

            // Generate embedding with completion/summary context
            var completionText = $"""
                {order.ToTextForEmbedding()}
                
                ORDER COMPLETION SUMMARY:
                Completed At: {message.CompletedAt:yyyy-MM-dd HH:mm}
                Invoice ID: {message.InvoiceId}
                Final Amount: {message.TotalAmount:F2} {order.CurrencyCode}
                
                Order Lifecycle Metrics:
                Total Cycle Time: {totalCycleTime:F1} days (from creation to invoice)
                Transit Time: {(deliveryTime.HasValue ? $"{deliveryTime:F1} days" : "N/A")}
                
                Customer Experience:
                Priority: {order.Priority}
                Delivery Notes: {order.DeliveryNotes ?? "None"}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(completionText, context.CancellationToken);

            // Build payload
            var payload = BuildOrderPayload(order, "Completed");
            payload["completedAt"] = message.CompletedAt.ToString("O");
            payload["invoiceId"] = message.InvoiceId?.ToString() ?? "";
            payload["finalAmount"] = (double)message.TotalAmount;
            payload["totalCycleDays"] = totalCycleTime;
            if (deliveryTime.HasValue)
                payload["transitDays"] = deliveryTime.Value;

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            _logger.LogInformation("Order {OrderId} COMPLETED. Total: {Amount} {Currency}. Cycle time: {CycleTime} days",
                message.OrderId, message.TotalAmount, order.CurrencyCode, totalCycleTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCompleted event for Order {OrderId}", message.OrderId);
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
            ["subtotal"] = (double)order.Subtotal,
            ["totalTax"] = (double)order.TotalTax,
            ["priority"] = order.Priority,
            ["lineCount"] = order.Lines.Count,
            ["productCodes"] = string.Join(", ", order.Lines.Select(l => l.ProductCode)),
            ["shippingCity"] = order.ShippingAddress?.City ?? "",
            ["shippingCountry"] = order.ShippingAddress?.CountryCode ?? "",
            ["recipientName"] = order.ShippingAddress?.RecipientName ?? "",
            ["trackingNumber"] = order.TrackingNumber ?? "",
            ["carrier"] = order.Carrier ?? "",
            ["createdAt"] = order.CreatedAt.ToString("O"),
            ["shippedAt"] = order.ShippedAt?.ToString("O") ?? "",
            ["deliveredAt"] = order.DeliveredAt?.ToString("O") ?? "",
            ["orderText"] = order.ToTextForEmbedding()
        };
    }
}
