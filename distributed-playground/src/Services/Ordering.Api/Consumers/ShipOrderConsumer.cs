using Contracts.Commands.Ordering;
using MassTransit;
using Ordering.Api.Services;

namespace Ordering.Api.Consumers;

public class ShipOrderConsumer : IConsumer<ShipOrder>
{
    private readonly OrderingService _orderingService;
    private readonly ILogger<ShipOrderConsumer> _logger;

    public ShipOrderConsumer(OrderingService orderingService, ILogger<ShipOrderConsumer> logger)
    {
        _orderingService = orderingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShipOrder> context)
    {
        _logger.LogInformation("Consuming ShipOrder command for order {OrderId}", context.Message.OrderId);

        try
        {
            var order = await _orderingService.ShipOrderAsync(context.Message, context.CancellationToken);
            
            _logger.LogInformation("Order {OrderId} shipped via MassTransit", order.Id);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = order.Id,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shipping order {OrderId}", context.Message.OrderId);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = context.Message.OrderId,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
