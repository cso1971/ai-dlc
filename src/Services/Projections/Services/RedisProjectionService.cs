using StackExchange.Redis;

namespace Projections.Services;

public class RedisProjectionService : IRedisProjectionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisProjectionService> _logger;

    private const string Prefix = "projections:orders";
    private const string TotalKey = $"{Prefix}:total";
    private const string StatusPrefix = $"{Prefix}:by-status";
    private const string CurrencyPrefix = $"{Prefix}:by-currency";
    private const string LastUpdatedKey = $"{Prefix}:last-updated";

    public RedisProjectionService(IConnectionMultiplexer redis, ILogger<RedisProjectionService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task IncrementOrderCountAsync(CancellationToken ct = default)
    {
        await Db.StringIncrementAsync(TotalKey);
    }

    public async Task IncrementOrdersByStatusAsync(string status, CancellationToken ct = default)
    {
        await Db.StringIncrementAsync($"{StatusPrefix}:{status}");
    }

    public async Task IncrementOrdersByCurrencyAsync(string currencyCode, decimal amount, CancellationToken ct = default)
    {
        var batch = Db.CreateBatch();
        _ = batch.StringIncrementAsync($"{CurrencyPrefix}:{currencyCode}:count");
        _ = batch.StringIncrementAsync($"{CurrencyPrefix}:{currencyCode}:value", (long)(amount * 100));
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task SetLastUpdatedAsync(CancellationToken ct = default)
    {
        await Db.StringSetAsync(LastUpdatedKey, DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task<long> GetOrderCountAsync(CancellationToken ct = default)
    {
        var val = await Db.StringGetAsync(TotalKey);
        return val.HasValue ? (long)val : 0;
    }

    public async Task<long> GetOrdersByStatusAsync(string status, CancellationToken ct = default)
    {
        var val = await Db.StringGetAsync($"{StatusPrefix}:{status}");
        return val.HasValue ? (long)val : 0;
    }

    public async Task<Dictionary<string, long>> GetAllOrdersByStatusAsync(CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var result = new Dictionary<string, long>();
        var pattern = $"{StatusPrefix}:*";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            var status = key.ToString().Replace($"{StatusPrefix}:", "");
            var val = await Db.StringGetAsync(key);
            if (val.HasValue)
                result[status] = (long)val;
        }

        return result;
    }

    public async Task<Dictionary<string, (long Count, decimal TotalValue)>> GetAllOrdersByCurrencyAsync(CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var result = new Dictionary<string, (long Count, decimal TotalValue)>();
        var pattern = $"{CurrencyPrefix}:*:count";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            var currency = key.ToString()
                .Replace($"{CurrencyPrefix}:", "")
                .Replace(":count", "");

            var count = await Db.StringGetAsync($"{CurrencyPrefix}:{currency}:count");
            var value = await Db.StringGetAsync($"{CurrencyPrefix}:{currency}:value");

            result[currency] = (
                Count: count.HasValue ? (long)count : 0,
                TotalValue: value.HasValue ? (long)value / 100m : 0m
            );
        }

        return result;
    }

    public async Task<string?> GetLastUpdatedAsync(CancellationToken ct = default)
    {
        var val = await Db.StringGetAsync(LastUpdatedKey);
        return val.HasValue ? val.ToString() : null;
    }

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
}
