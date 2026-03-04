namespace Orchestrator.Api.Clients;

public class CustomersApiClient : ICustomersApiClient
{
    private readonly HttpClient _http;

    public CustomersApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> GetCustomersJsonAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("api/customers", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
