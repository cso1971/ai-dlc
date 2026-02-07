namespace Orchestrator.Api.Clients;

public class OrderingApiClient : IOrderingApiClient
{
    private readonly HttpClient _http;

    public OrderingApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> GetOrdersJsonAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("api/orders", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string> GetOrderStatsJsonAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("api/orders/stats", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> GetOrderByIdJsonAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"api/orders/{orderId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
