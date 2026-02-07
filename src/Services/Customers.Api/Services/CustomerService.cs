using Contracts.Commands.Customers;
using Contracts.Events.Customers;
using Customers.Api.Domain;
using MassTransit;

namespace Customers.Api.Services;

public class CustomerService
{
    private readonly ICustomersRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(ICustomersRepository repository, IPublishEndpoint publishEndpoint, ILogger<CustomerService> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<Customer> CreateCustomerAsync(CreateCustomer command, CancellationToken cancellationToken = default)
    {
        var customer = Customer.FromCommand(command);
        await _repository.AddAsync(customer, cancellationToken);
        await _publishEndpoint.Publish(new CustomerCreated
        {
            CustomerId = customer.Id,
            CompanyName = customer.CompanyName,
            DisplayName = customer.DisplayName,
            Email = customer.Email,
            CreatedAt = customer.CreatedAt
        }, cancellationToken);
        _logger.LogInformation("Customer {CustomerId} created and persisted", customer.Id);
        return customer;
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    public async Task<Customer?> UpdateCustomerAsync(UpdateCustomer command, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer == null) return null;
        customer.ApplyUpdate(command);
        await _repository.UpdateAsync(customer, cancellationToken);
        await _publishEndpoint.Publish(new CustomerUpdated
        {
            CustomerId = customer.Id,
            CompanyName = customer.CompanyName,
            DisplayName = customer.DisplayName,
            Email = customer.Email,
            UpdatedAt = customer.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);
        _logger.LogInformation("Customer {CustomerId} updated", command.CustomerId);
        return customer;
    }

    public async Task<Customer?> CancelCustomerAsync(CancelCustomer command, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer == null) return null;
        customer.Cancel(command.CancellationReason);
        await _repository.UpdateAsync(customer, cancellationToken);
        if (customer.CancelledAt.HasValue)
        {
            await _publishEndpoint.Publish(new CustomerCancelled
            {
                CustomerId = customer.Id,
                CancellationReason = customer.CancellationReason ?? command.CancellationReason,
                CancelledAt = customer.CancelledAt.Value
            }, cancellationToken);
        }
        _logger.LogInformation("Customer {CustomerId} cancelled", command.CustomerId);
        return customer;
    }
}
