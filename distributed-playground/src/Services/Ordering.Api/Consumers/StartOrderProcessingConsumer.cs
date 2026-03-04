using Contracts.Commands.Ordering;
using MassTransit;
using Ordering.Api.Services;

namespace Ordering.Api.Consumers;

public class StartOrderProcessingConsumer : IConsumer<StartOrderProcessing>
{
    private readonly OrderingService _orderingService;
    private readonly ILogger<StartOrderProcessingConsumer> _logger;

    public StartOrderProcessingConsumer(OrderingService orderingService, ILogger<StartOrderProcessingConsumer> logger)
    {
        _orderingService = orderingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StartOrderProcessing> context)
    {
        _logger.LogInformation("Consuming StartOrderProcessing command for order {OrderId}", context.Message.OrderId);

        try
        {
            var order = await _orderingService.StartProcessingAsync(context.Message, context.CancellationToken);
            
            _logger.LogInformation("Order {OrderId} processing started via MassTransit", order.Id);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = order.Id,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting processing for order {OrderId}", context.Message.OrderId);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = context.Message.OrderId,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
