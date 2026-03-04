using Contracts.Commands.Ordering;
using MassTransit;
using Ordering.Api.Services;

namespace Ordering.Api.Consumers;

public class InvoiceOrderConsumer : IConsumer<InvoiceOrder>
{
    private readonly OrderingService _orderingService;
    private readonly ILogger<InvoiceOrderConsumer> _logger;

    public InvoiceOrderConsumer(OrderingService orderingService, ILogger<InvoiceOrderConsumer> logger)
    {
        _orderingService = orderingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InvoiceOrder> context)
    {
        _logger.LogInformation("Consuming InvoiceOrder command for order {OrderId}", context.Message.OrderId);

        try
        {
            var order = await _orderingService.InvoiceOrderAsync(context.Message, context.CancellationToken);
            
            _logger.LogInformation("Order {OrderId} invoiced via MassTransit", order.Id);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = order.Id,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoicing order {OrderId}", context.Message.OrderId);

            await context.RespondAsync(new OrderCommandResponse
            {
                OrderId = context.Message.OrderId,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
