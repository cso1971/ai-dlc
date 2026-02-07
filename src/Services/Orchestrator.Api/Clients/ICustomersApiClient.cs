namespace Orchestrator.Api.Clients;

/// <summary>
/// Minimal client for Customers API (used by Semantic Kernel plugins).
/// </summary>
public interface ICustomersApiClient
{
    Task<string> GetCustomersJsonAsync(CancellationToken cancellationToken = default);
}
