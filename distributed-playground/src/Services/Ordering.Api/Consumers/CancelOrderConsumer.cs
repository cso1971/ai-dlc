using Contracts.Commands.Ordering;
using MassTransit;
using Ordering.Api.Services;

namespace Ordering.Api.Consumers;

public class CancelOrderConsumer : IConsumer<CancelOrder>
{
    private readonly OrderingService _orderingService;
    private readonly ILogger<CancelOrderConsumer> _logger;

    public CancelOrderConsumer(OrderingService orderingService, ILogger<CancelOrderConsumer> logger)
    {
        _orderingService = orderingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CancelOrder> context)
    {
        _logger.LogInformation("Consuming CancelOrder command for order {OrderId}", context.Message.OrderId);

        try
        {
            var order = await _orderingService.CancelOrderAsync(context.Message, context.CancellationToken);
            
            _logger.LogInformation("Order {OrderId} cancelled via MassTransit", order.Id);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = order.Id,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", context.Message.OrderId);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = context.Message.OrderId,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
