using Contracts.Commands.Ordering;
using MassTransit;
using Ordering.Api.Services;

namespace Ordering.Api.Consumers;

public class CreateOrderConsumer : IConsumer<CreateOrder>
{
    private readonly OrderingService _orderingService;
    private readonly ILogger<CreateOrderConsumer> _logger;

    public CreateOrderConsumer(OrderingService orderingService, ILogger<CreateOrderConsumer> logger)
    {
        _orderingService = orderingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateOrder> context)
    {
        _logger.LogInformation("Consuming CreateOrder command for customer {CustomerId}", context.Message.CustomerId);

        try
        {
            var order = await _orderingService.CreateOrderAsync(context.Message, context.CancellationToken);
            
            _logger.LogInformation("Order {OrderId} created via MassTransit", order.Id);

            await context.RespondAsync(new CreateOrderResponse
            {
                OrderId = order.Id,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", context.Message.CustomerId);

            await context.RespondAsync(new CreateOrderResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

public record CreateOrderResponse
{
    public Guid OrderId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
