namespace Orchestrator.Api.Clients;

/// <summary>
/// Minimal client for Ordering API (used by Semantic Kernel plugins).
/// </summary>
public interface IOrderingApiClient
{
    Task<string> GetOrdersJsonAsync(CancellationToken cancellationToken = default);
    Task<string> GetOrderStatsJsonAsync(CancellationToken cancellationToken = default);
    Task<string?> GetOrderByIdJsonAsync(Guid orderId, CancellationToken cancellationToken = default);
}
