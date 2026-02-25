namespace Projections.Services;

public interface IRedisProjectionService
{
    // --- Snapshot (order state stored for cross-event lookups) ---
    Task SaveOrderSnapshotAsync(Guid orderId, OrderSnapshot snapshot, CancellationToken ct = default);
    Task<OrderSnapshot?> GetOrderSnapshotAsync(Guid orderId, CancellationToken ct = default);
    Task UpdateSnapshotStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default);
    Task UpdateSnapshotDeliveredAtAsync(Guid orderId, DateTime deliveredAt, CancellationToken ct = default);

    // --- Dimension aggregation (generic: count + subtotal + grandtotal) ---
    Task IncrementDimensionAsync(string dimension, string key, decimal subtotal, decimal grandTotal, CancellationToken ct = default);
    Task DecrementDimensionAsync(string dimension, string key, decimal subtotal, decimal grandTotal, CancellationToken ct = default);
    Task<Dictionary<string, DimensionStats>> GetAllByDimensionAsync(string dimension, CancellationToken ct = default);

    // --- Global counters ---
    Task IncrementOrderCountAsync(CancellationToken ct = default);
    Task<long> GetOrderCountAsync(CancellationToken ct = default);

    // --- Metadata ---
    Task SetLastUpdatedAsync(CancellationToken ct = default);
    Task<string?> GetLastUpdatedAsync(CancellationToken ct = default);

    // --- Flush ---
    Task FlushProjectionsAsync(CancellationToken ct = default);
}

public record OrderSnapshot
{
    public string Status { get; init; } = "";
    public string CustomerReference { get; init; } = "";
    public string ShippingMethod { get; init; } = "";
    public string CurrencyCode { get; init; } = "";
    public decimal Subtotal { get; init; }
    public decimal GrandTotal { get; init; }
    public string CreatedAtMonth { get; init; } = "";
    public string CreatedAtYear { get; init; } = "";
    public string? DeliveredAtMonth { get; init; }
    public string? DeliveredAtYear { get; init; }
    public List<string> LineDescriptions { get; init; } = [];
}

public record DimensionStats
{
    public long Count { get; init; }
    public decimal Subtotal { get; init; }
    public decimal GrandTotal { get; init; }
}
