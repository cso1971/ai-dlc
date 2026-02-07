using Contracts.Commands.Customers;
using Customers.Api.Domain;

namespace Customers.Api.Services;

public class CustomerService
{
    private readonly ICustomersRepository _repository;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(ICustomersRepository repository, ILogger<CustomerService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Customer> CreateCustomerAsync(CreateCustomer command, CancellationToken cancellationToken = default)
    {
        var customer = Customer.FromCommand(command);
        await _repository.AddAsync(customer, cancellationToken);
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
}
