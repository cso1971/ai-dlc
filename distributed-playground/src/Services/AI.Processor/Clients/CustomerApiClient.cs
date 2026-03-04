using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace AI.Processor.Clients;

public class CustomerApiClient : ICustomerApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CustomerApiClient> _logger;

    public CustomerApiClient(HttpClient httpClient, ILogger<CustomerApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CustomerResponse?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching customer {CustomerId} from Customers API", customerId);

            var response = await _httpClient.GetAsync($"api/customers/{customerId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch customer {CustomerId}: {StatusCode}", customerId, response.StatusCode);
                return null;
            }

            var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken: cancellationToken);

            _logger.LogDebug("Successfully fetched customer {CustomerId} ({CompanyName})", customerId, customer?.CompanyName);

            return customer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer {CustomerId} from Customers API", customerId);
            return null;
        }
    }
}
