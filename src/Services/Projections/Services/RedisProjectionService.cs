using System.Text.Json;
using StackExchange.Redis;

namespace Projections.Services;

public class RedisProjectionService : IRedisProjectionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisProjectionService> _logger;

    private const string Prefix = "projections:orders";
    private const string TotalKey = $"{Prefix}:total";
    private const string SnapshotPrefix = $"{Prefix}:snapshot";
    private const string DimPrefix = $"{Prefix}:dim";
    private const string LastUpdatedKey = $"{Prefix}:last-updated";

    public RedisProjectionService(IConnectionMultiplexer redis, ILogger<RedisProjectionService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    // ========== Snapshot ==========

    public async Task SaveOrderSnapshotAsync(Guid orderId, OrderSnapshot snapshot, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(snapshot);
        await Db.StringSetAsync($"{SnapshotPrefix}:{orderId}", json);
    }

    public async Task<OrderSnapshot?> GetOrderSnapshotAsync(Guid orderId, CancellationToken ct = default)
    {
        var val = await Db.StringGetAsync($"{SnapshotPrefix}:{orderId}");
        if (!val.HasValue) return null;
        return JsonSerializer.Deserialize<OrderSnapshot>(val.ToString());
    }

    public async Task UpdateSnapshotStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default)
    {
        var snapshot = await GetOrderSnapshotAsync(orderId, ct);
        if (snapshot == null) return;
        var updated = snapshot with { Status = newStatus };
        await SaveOrderSnapshotAsync(orderId, updated, ct);
    }

    public async Task UpdateSnapshotDeliveredAtAsync(Guid orderId, DateTime deliveredAt, CancellationToken ct = default)
    {
        var snapshot = await GetOrderSnapshotAsync(orderId, ct);
        if (snapshot == null) return;
        var updated = snapshot with
        {
            DeliveredAtMonth = deliveredAt.ToString("yyyy-MM"),
            DeliveredAtYear = deliveredAt.ToString("yyyy")
        };
        await SaveOrderSnapshotAsync(orderId, updated, ct);
    }

    // ========== Dimension Aggregation ==========

    public async Task IncrementDimensionAsync(string dimension, string key, decimal subtotal, decimal grandTotal, CancellationToken ct = default)
    {
        var prefix = $"{DimPrefix}:{dimension}:{SanitizeKey(key)}";
        var batch = Db.CreateBatch();
        _ = batch.StringIncrementAsync($"{prefix}:count");
        _ = batch.StringIncrementAsync($"{prefix}:subtotal", (long)(subtotal * 100));
        _ = batch.StringIncrementAsync($"{prefix}:grandtotal", (long)(grandTotal * 100));
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task DecrementDimensionAsync(string dimension, string key, decimal subtotal, decimal grandTotal, CancellationToken ct = default)
    {
        var prefix = $"{DimPrefix}:{dimension}:{SanitizeKey(key)}";
        var batch = Db.CreateBatch();
        _ = batch.StringDecrementAsync($"{prefix}:count");
        _ = batch.StringDecrementAsync($"{prefix}:subtotal", (long)(subtotal * 100));
        _ = batch.StringDecrementAsync($"{prefix}:grandtotal", (long)(grandTotal * 100));
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task<Dictionary<string, DimensionStats>> GetAllByDimensionAsync(string dimension, CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var result = new Dictionary<string, DimensionStats>();
        var pattern = $"{DimPrefix}:{dimension}:*:count";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            var dimKey = key.ToString()
                .Replace($"{DimPrefix}:{dimension}:", "")
                .Replace(":count", "");

            var countVal = await Db.StringGetAsync($"{DimPrefix}:{dimension}:{dimKey}:count");
            var subVal = await Db.StringGetAsync($"{DimPrefix}:{dimension}:{dimKey}:subtotal");
            var grandVal = await Db.StringGetAsync($"{DimPrefix}:{dimension}:{dimKey}:grandtotal");

            result[dimKey] = new DimensionStats
            {
                Count = countVal.HasValue ? (long)countVal : 0,
                Subtotal = subVal.HasValue ? (long)subVal / 100m : 0m,
                GrandTotal = grandVal.HasValue ? (long)grandVal / 100m : 0m
            };
        }

        return result;
    }

    // ========== Global Counters ==========

    public async Task IncrementOrderCountAsync(CancellationToken ct = default)
    {
        await Db.StringIncrementAsync(TotalKey);
    }

    public async Task<long> GetOrderCountAsync(CancellationToken ct = default)
    {
        var val = await Db.StringGetAsync(TotalKey);
        return val.HasValue ? (long)val : 0;
    }

    // ========== Metadata ==========

    public async Task SetLastUpdatedAsync(CancellationToken ct = default)
    {
        await Db.StringSetAsync(LastUpdatedKey, DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task<string?> GetLastUpdatedAsync(CancellationToken ct = default)
    {
        var val = await Db.StringGetAsync(LastUpdatedKey);
        return val.HasValue ? val.ToString() : null;
    }

    // ========== Flush ==========

    public async Task FlushProjectionsAsync(CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keysToDelete = new List<RedisKey>();

        await foreach (var key in server.KeysAsync(pattern: $"{Prefix}:*"))
        {
            keysToDelete.Add(key);
        }

        if (keysToDelete.Count > 0)
        {
            await Db.KeyDeleteAsync(keysToDelete.ToArray());
            _logger.LogInformation("Flushed {Count} projection keys from Redis", keysToDelete.Count);
        }
    }

    private static string SanitizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "_empty_";
        return key.Replace(":", "_").Replace(" ", "_");
    }
}
