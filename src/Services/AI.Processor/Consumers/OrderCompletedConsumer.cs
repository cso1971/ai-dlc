using System.Text.Json;
using AI.Processor.Services;
using Contracts.Events.Ordering;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Consumers;

public class OrderCompletedConsumer : IConsumer<OrderCompleted>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<OrderCompletedConsumer> _logger;

    public OrderCompletedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ILogger<OrderCompletedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCompleted> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing OrderCompleted event for Order {OrderId}, Invoice: {InvoiceId}", 
            message.OrderId, message.InvoiceId);

        try
        {
            // Create completion details for embedding
            var completionDetails = $"""
                Order completed (invoiced) at {message.CompletedAt}
                Invoice ID: {message.InvoiceId}
                Total Amount: {message.TotalAmount:F2}
                """;

            var embedding = await _ollamaService.GenerateEmbeddingAsync(completionDetails, context.CancellationToken);

            var payload = new Dictionary<string, object>
            {
                ["status"] = "Completed",
                ["completedAt"] = message.CompletedAt.ToString("O"),
                ["invoiceId"] = message.InvoiceId?.ToString() ?? "",
                ["totalAmount"] = message.TotalAmount
            };

            await _qdrantService.UpsertOrderAsync(message.OrderId, embedding, payload, context.CancellationToken);

            // Generate order summary
            var summary = await _ollamaService.SummarizeOrderAsync(
                message.OrderId,
                completionDetails,
                context.CancellationToken);

            _logger.LogInformation("Order {OrderId} completion processed. Summary: {Summary}", 
                message.OrderId, summary.Substring(0, Math.Min(200, summary.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCompleted event for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
