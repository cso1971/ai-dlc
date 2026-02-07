using Contracts.Commands.Customers;
using Contracts.Events.Customers;
using MassTransit;
using Customers.Api.Services;

namespace Customers.Api.Consumers;

public class UpdateCustomerConsumer : IConsumer<UpdateCustomer>
{
    private readonly CustomerService _customerService;
    private readonly ILogger<UpdateCustomerConsumer> _logger;

    public UpdateCustomerConsumer(CustomerService customerService, ILogger<UpdateCustomerConsumer> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateCustomer> context)
    {
        var message = context.Message;
        _logger.LogInformation("Consuming UpdateCustomer command for {CustomerId}", message.CustomerId);

        try
        {
            var customer = await _customerService.UpdateCustomerAsync(message, context.CancellationToken);

            if (customer == null)
            {
                await context.RespondAsync(new UpdateCustomerResponse
                {
                    Success = false,
                    ErrorMessage = $"Customer {message.CustomerId} not found"
                });
                return;
            }

            await context.Publish(new CustomerUpdated
            {
                CustomerId = customer.Id,
                CompanyName = customer.CompanyName,
                DisplayName = customer.DisplayName,
                Email = customer.Email,
                UpdatedAt = customer.UpdatedAt ?? DateTime.UtcNow
            });

            _logger.LogInformation("Customer {CustomerId} updated via MassTransit", customer.Id);

            await context.RespondAsync(new UpdateCustomerResponse
            {
                CustomerId = customer.Id,
                Success = true,
                UpdatedAt = customer.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer {CustomerId}", message.CustomerId);

            await context.RespondAsync(new UpdateCustomerResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

public record UpdateCustomerResponse
{
    public Guid CustomerId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
