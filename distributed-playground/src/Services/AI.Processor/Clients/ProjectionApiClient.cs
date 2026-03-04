using System.Net.Http.Json;

namespace AI.Processor.Clients;

public class ProjectionApiClient : IProjectionApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProjectionApiClient> _logger;

    public ProjectionApiClient(HttpClient httpClient, ILogger<ProjectionApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProjectionStatsResponse?> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching projection stats from Projections API");
            var response = await _httpClient.GetAsync("api/projections/stats", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch projection stats: {StatusCode}", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ProjectionStatsResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Projections API unreachable, RAG will use fallback order stats");
            return null;
        }
    }
}
