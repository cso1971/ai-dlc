using Contracts.Commands.Customers;
using MassTransit;
using Customers.Api.Services;

namespace Customers.Api.Consumers;

public class CreateCustomerConsumer : IConsumer<CreateCustomer>
{
    private readonly CustomerService _customerService;
    private readonly ILogger<CreateCustomerConsumer> _logger;

    public CreateCustomerConsumer(CustomerService customerService, ILogger<CreateCustomerConsumer> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateCustomer> context)
    {
        _logger.LogInformation("Consuming CreateCustomer command for {Email}", context.Message.Email);

        try
        {
            var customer = await _customerService.CreateCustomerAsync(context.Message, context.CancellationToken);
            // CustomerCreated is published by CustomerService

            _logger.LogInformation("Customer {CustomerId} created via MassTransit and persisted", customer.Id);

            await context.RespondAsync(new CreateCustomerResponse
            {
                CustomerId = customer.Id,
                Success = true,
                CreatedAt = customer.CreatedAt
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
