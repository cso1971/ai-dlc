using Microsoft.Extensions.Logging;

namespace Ordering.Api.Clients;

public class CustomersApiClient : ICustomersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CustomersApiClient> _logger;

    public CustomersApiClient(HttpClient httpClient, ILogger<CustomersApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/customers/{customerId}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;
            if (response.IsSuccessStatusCode)
                return true;
            _logger.LogWarning("Customers API returned {StatusCode} for customer {CustomerId}", response.StatusCode, customerId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking customer {CustomerId} in Customers API", customerId);
            throw;
        }
    }
}
