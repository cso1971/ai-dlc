using Contracts.Events.Ordering;
using MassTransit;
using Projections.Services;

namespace Projections.Consumers;

public class OrderCreatedProjectionConsumer : IConsumer<OrderCreated>
{
    private readonly IRedisProjectionService _projection;
    private readonly ILogger<OrderCreatedProjectionConsumer> _logger;

    public OrderCreatedProjectionConsumer(
        IRedisProjectionService projection,
        ILogger<OrderCreatedProjectionConsumer> logger)
    {
        _projection = projection;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Projecting OrderCreated for Order {OrderId}, Currency={Currency}, Amount={Amount}",
            message.OrderId, message.CurrencyCode, message.TotalAmount);

        try
        {
            await _projection.IncrementOrderCountAsync(context.CancellationToken);
            await _projection.IncrementOrdersByStatusAsync("Created", context.CancellationToken);
            await _projection.IncrementOrdersByCurrencyAsync(message.CurrencyCode, message.TotalAmount, context.CancellationToken);
            await _projection.SetLastUpdatedAsync(context.CancellationToken);

            _logger.LogInformation("Projection updated for Order {OrderId}", message.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting OrderCreated for Order {OrderId}", message.OrderId);
            throw;
        }
    }
}
