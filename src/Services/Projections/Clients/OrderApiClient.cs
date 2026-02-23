using System.Net.Http.Json;

namespace Projections.Clients;

public interface IOrderApiClient
{
    Task<OrderDetail?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
}

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderApiClient> _logger;

    public OrderApiClient(HttpClient httpClient, ILogger<OrderApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderDetail?> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/orders/{orderId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch order {OrderId}: {StatusCode}", orderId, response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<OrderDetail>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching order {OrderId}", orderId);
            return null;
        }
    }
}

public record OrderDetail
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerReference { get; init; }
    public string CurrencyCode { get; init; } = "";
    public string? ShippingMethod { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal Subtotal { get; init; }
    public decimal GrandTotal { get; init; }
    public List<OrderLineDetail> Lines { get; init; } = [];
}

public record OrderLineDetail
{
    public string Description { get; init; } = "";
    public decimal LineTotal { get; init; }
    public decimal LineTotalWithTax { get; init; }
}
