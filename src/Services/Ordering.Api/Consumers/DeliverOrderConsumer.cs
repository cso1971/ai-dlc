using Contracts.Commands.Ordering;
using MassTransit;
using Ordering.Api.Services;

namespace Ordering.Api.Consumers;

public class DeliverOrderConsumer : IConsumer<DeliverOrder>
{
    private readonly OrderingService _orderingService;
    private readonly ILogger<DeliverOrderConsumer> _logger;

    public DeliverOrderConsumer(OrderingService orderingService, ILogger<DeliverOrderConsumer> logger)
    {
        _orderingService = orderingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeliverOrder> context)
    {
        _logger.LogInformation("Consuming DeliverOrder command for order {OrderId}", context.Message.OrderId);

        try
        {
            var order = await _orderingService.DeliverOrderAsync(context.Message, context.CancellationToken);
            
            _logger.LogInformation("Order {OrderId} delivered via MassTransit", order.Id);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = order.Id,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering order {OrderId}", context.Message.OrderId);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = context.Message.OrderId,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
