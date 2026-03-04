namespace AI.Processor.Clients;

public interface ICustomerApiClient
{
    Task<CustomerResponse?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
