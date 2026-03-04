namespace Ordering.Api.Clients;

/// <summary>
/// Client for the Customers bounded context API. Used to validate that a customer exists when creating an order.
/// </summary>
public interface ICustomersApiClient
{
    /// <summary>
    /// Returns true if a customer with the given id exists in the Customers context.
    /// </summary>
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default);
}
