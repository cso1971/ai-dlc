using System.Text.Json;
using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderCancelledConsumer : IConsumer<OrderCancelled>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly IOrderApiClient _orderApiClient;
    private readonly ILogger<OrderCancelledConsumer> _logger;

    public OrderCancelledConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        IOrderApiClient orderApiClient,
        ILogger<OrderCancelledConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _orderApiClient = orderApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCancelled event for Order {OrderId}, Reason: {Reason}", 
            message.OrderId, message.CancellationReason);

        try
        {
            // Fetch complete order data
            var order = await _orderApiClient.GetOrderAsync(message.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Could not fetch order {OrderId} from API", message.OrderId);
                return;
            }

            // Calculate order age at cancellation
            var orderAge = (message.CancelledAt - order.CreatedAt).TotalDays;

            // Generate embedding with cancellation context - important for business insights
            var cancellationText = $"""
                {order.ToTextForEmbedding()}
                
                CANCELLATION EVENT:
                Cancelled At: {message.CancelledAt:yyyy-MM-dd HH:mm}
                Cancelled By: {message.CancelledBy ?? "Unknown"}
                Status When Cancelled: {message.StatusWhenCancelled}
                
                CANCELLATION REASON:
                {message.CancellationReason}
                
                Order Metrics at Cancellation:
                Order Age: {orderAge:F1} days
                Order Value Lost: {order.GrandTotal:F2} {order.CurrencyCode}
                Products Affected: {string.Join(", ", order.Lines.Select(l => l.ProductCode))}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(cancellationText, context.CancellationToken);

            // Build payload with cancellation-specific data
            var payload = BuildOrderPayload(order, "Cancelled");
            payload["cancelledAt"] = message.CancelledAt.ToString("O");
            payload["cancelledBy"] = message.CancelledBy ?? "";
            payload["cancellationReason"] = message.CancellationReason;
            payload["statusWhenCancelled"] = message.StatusWhenCancelled.ToString();
            payload["orderAgeDays"] = orderAge;
            payload["valueLost"] = (double)order.GrandTotal;

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            _logger.LogWarning("Order {OrderId} CANCELLED. Value lost: {GrandTotal} {Currency}. Reason: {Reason}",
                message.OrderId, order.GrandTotal, order.CurrencyCode, message.CancellationReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCancelled event for Order {OrderId}", message.OrderId);
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
            ["createdAt"] = order.CreatedAt.ToString("O"),
            ["orderText"] = order.ToTextForEmbedding()
        };
    }
}
