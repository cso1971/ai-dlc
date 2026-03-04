using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace AI.Processor.Clients;

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderApiClient> _logger;

    public OrderApiClient(HttpClient httpClient, ILogger<OrderApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderResponse?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching order {OrderId} from Ordering API", orderId);
            
            var response = await _httpClient.GetAsync($"api/orders/{orderId}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch order {OrderId}: {StatusCode}", orderId, response.StatusCode);
                return null;
            }

            var order = await response.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken: cancellationToken);
            
            _logger.LogDebug("Successfully fetched order {OrderId} with {LineCount} lines, total {GrandTotal}", 
                orderId, order?.Lines.Count ?? 0, order?.GrandTotal ?? 0);
            
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching order {OrderId} from Ordering API", orderId);
            return null;
        }
    }

    public async Task<OrderStatsResponse?> GetOrderStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/orders/stats", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch order stats: {StatusCode}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<OrderStatsResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching order stats from Ordering API");
            return null;
        }
    }

    /// <summary>
    /// Creates the HTTP client with retry policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
