using System.Text.Json;
using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderStatusChangedConsumer : IConsumer<OrderStatusChanged>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly IOrderApiClient _orderApiClient;
    private readonly ILogger<OrderStatusChangedConsumer> _logger;

    public OrderStatusChangedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        IOrderApiClient orderApiClient,
        ILogger<OrderStatusChangedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _orderApiClient = orderApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderStatusChanged event for Order {OrderId}: {PreviousStatus} -> {NewStatus}", 
            message.OrderId, message.PreviousStatus, message.NewStatus);

        try
        {
            // Fetch complete order data from Ordering API
            var order = await _orderApiClient.GetOrderAsync(message.OrderId, context.CancellationToken);
            
            if (order == null)
            {
                _logger.LogWarning("Could not fetch order {OrderId} from API", message.OrderId);
                return;
            }

            // Generate embedding with status change context
            var statusChangeText = $"""
                {order.ToTextForEmbedding()}
                
                Status Change Event:
                From: {message.PreviousStatus}
                To: {message.NewStatus}
                Changed At: {message.ChangedAt:yyyy-MM-dd HH:mm}
                Reason: {message.Reason ?? "N/A"}
                Changed By: {message.ChangedBy ?? "System"}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(statusChangeText, context.CancellationToken);

            // Build payload
            var payload = BuildOrderPayload(order, $"StatusChanged_{message.NewStatus}");
            payload["previousStatus"] = message.PreviousStatus.ToString();
            payload["statusChangeReason"] = message.Reason ?? "";
            payload["statusChangedBy"] = message.ChangedBy ?? "System";
            payload["statusChangedAt"] = message.ChangedAt.ToString("O");

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Analyze the status transition
            var analysis = await _ollamaService.AnalyzeOrderEventAsync(
                $"Status Change: {message.PreviousStatus} → {message.NewStatus}",
                statusChangeText,
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} status change processed. Analysis: {Analysis}", 
                message.OrderId, analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderStatusChanged event for Order {OrderId}", message.OrderId);
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
