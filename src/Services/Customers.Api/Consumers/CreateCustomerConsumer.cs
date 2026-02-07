using Contracts.Commands.Customers;
using Contracts.Events.Customers;
using MassTransit;

namespace Customers.Api.Consumers;

public class CreateCustomerConsumer : IConsumer<CreateCustomer>
{
    private readonly ILogger<CreateCustomerConsumer> _logger;

    public CreateCustomerConsumer(ILogger<CreateCustomerConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateCustomer> context)
    {
        _logger.LogInformation("Consuming CreateCustomer command for {Email}", context.Message.Email);

        try
        {
            // Placeholder: genera un Id simulato (in un secondo step il CustomerService/aggregate persisterà)
            var customerId = Guid.NewGuid();

            await context.Publish(new CustomerCreated
            {
                CustomerId = customerId,
                CompanyName = context.Message.CompanyName,
                DisplayName = context.Message.DisplayName,
                Email = context.Message.Email,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Customer {CustomerId} created via MassTransit", customerId);

            await context.RespondAsync(new CreateCustomerResponse
            {
                CustomerId = customerId,
                Success = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer for {Email}", context.Message.Email);

            await context.RespondAsync(new CreateCustomerResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

public record CreateCustomerResponse
{
    public Guid CustomerId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? CreatedAt { get; init; }
}
