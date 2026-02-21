namespace Projections.Services;

public interface IRedisProjectionService
{
    Task IncrementOrderCountAsync(CancellationToken ct = default);
    Task IncrementOrdersByStatusAsync(string status, CancellationToken ct = default);
    Task IncrementOrdersByCurrencyAsync(string currencyCode, decimal amount, CancellationToken ct = default);
    Task SetLastUpdatedAsync(CancellationToken ct = default);

    Task<long> GetOrderCountAsync(CancellationToken ct = default);
    Task<long> GetOrdersByStatusAsync(string status, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetAllOrdersByStatusAsync(CancellationToken ct = default);
    Task<Dictionary<string, (long Count, decimal TotalValue)>> GetAllOrdersByCurrencyAsync(CancellationToken ct = default);
    Task<string?> GetLastUpdatedAsync(CancellationToken ct = default);

    Task FlushProjectionsAsync(CancellationToken ct = default);
}
