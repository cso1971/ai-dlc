using Contracts.Commands.Customers;
using Contracts.Events.Customers;
using MassTransit;
using Customers.Api.Services;

namespace Customers.Api.Consumers;

public class CancelCustomerConsumer : IConsumer<CancelCustomer>
{
    private readonly CustomerService _customerService;
    private readonly ILogger<CancelCustomerConsumer> _logger;

    public CancelCustomerConsumer(CustomerService customerService, ILogger<CancelCustomerConsumer> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CancelCustomer> context)
    {
        var message = context.Message;
        _logger.LogInformation("Consuming CancelCustomer command for {CustomerId}", message.CustomerId);

        try
        {
            var customer = await _customerService.CancelCustomerAsync(message, context.CancellationToken);

            if (customer == null)
            {
                await context.RespondAsync(new CancelCustomerResponse
                {
                    Success = false,
                    ErrorMessage = $"Customer {message.CustomerId} not found"
                });
                return;
            }

            if (customer.CancelledAt.HasValue)
            {
                await context.Publish(new CustomerCancelled
                {
                    CustomerId = customer.Id,
                    CancellationReason = customer.CancellationReason ?? message.CancellationReason,
                    CancelledAt = customer.CancelledAt.Value
                });
                _logger.LogInformation("Customer {CustomerId} cancelled via MassTransit", customer.Id);
            }

            await context.RespondAsync(new CancelCustomerResponse
            {
                CustomerId = customer.Id,
                Success = true,
                CancelledAt = customer.CancelledAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling customer {CustomerId}", message.CustomerId);

            await context.RespondAsync(new CancelCustomerResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

public record CancelCustomerResponse
{
    public Guid CustomerId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? CancelledAt { get; init; }
}
