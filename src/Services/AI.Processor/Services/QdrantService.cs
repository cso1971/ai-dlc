using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AI.Processor.Services;

public class QdrantService : IQdrantService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly string _customersCollectionName;
    private readonly ILogger<QdrantService> _logger;
    private const int VectorSize = 768; // nomic-embed-text dimension

    public QdrantService(IConfiguration configuration, ILogger<QdrantService> logger)
    {
        _logger = logger;
        var host = configuration["Qdrant:Host"] ?? "localhost";
        var port = int.Parse(configuration["Qdrant:Port"] ?? "6334");
        _collectionName = configuration["Qdrant:CollectionName"] ?? "orders";
        _customersCollectionName = configuration["Qdrant:CustomersCollectionName"] ?? "customers";

        _client = new QdrantClient(host, port);
        _logger.LogInformation("QdrantService initialized with endpoint {Host}:{Port}, collections: {Orders}, {Customers}",
            host, port, _collectionName, _customersCollectionName);
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            if (collections.Any(c => c == _collectionName))
            {
                _logger.LogDebug("Collection {Collection} already exists", _collectionName);
                return;
            }

            await _client.CreateCollectionAsync(
                _collectionName,
                new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created collection {Collection} with vector size {Size}", _collectionName, VectorSize);
        }
        catch (Exception ex)
        {
            // Race condition: another consumer created the collection between List and Create
            if (ex.Message.Contains("AlreadyExists", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Collection {Collection} already exists (created by concurrent consumer)", _collectionName);
                return;
            }
            _logger.LogError(ex, "Error ensuring collection exists");
            throw;
        }
    }

    public async Task UpsertOrderAsync(Guid orderId, float[] embedding, Dictionary<string, object> payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCollectionExistsAsync(cancellationToken);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = orderId.ToString() },
                Vectors = embedding
            };

            foreach (var (key, value) in payload)
            {
                point.Payload[key] = value switch
                {
                    string s => s,
                    int i => i,
                    long l => l,
                    double d => d,
                    bool b => b,
                    DateTime dt => dt.ToString("O"),
                    Guid g => g.ToString(),
                    _ => value.ToString() ?? string.Empty
                };
            }

            await _client.UpsertAsync(_collectionName, [point], cancellationToken: cancellationToken);
            _logger.LogDebug("Upserted order {OrderId} to collection {Collection}", orderId, _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<IReadOnlyList<SearchResult>> SearchSimilarOrdersAsync(float[] embedding, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCollectionExistsAsync(cancellationToken);

            var searchResults = await _client.SearchAsync(
                _collectionName,
                embedding,
                limit: (ulong)limit,
                cancellationToken: cancellationToken);

            var results = searchResults.Select(r => new SearchResult(
                Guid.Parse(r.Id.Uuid),
                r.Score,
                r.Payload.ToDictionary(
                    p => p.Key,
                    p => (object)(p.Value.StringValue ?? p.Value.IntegerValue.ToString()))
            )).ToList();

            _logger.LogDebug("Found {Count} similar orders", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar orders");
            throw;
        }
    }

    public async Task<IReadOnlyList<CustomerSearchResult>> SearchSimilarCustomersAsync(float[] embedding, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCustomersCollectionExistsAsync(cancellationToken);

            var searchResults = await _client.SearchAsync(
                _customersCollectionName,
                embedding,
                limit: (ulong)limit,
                cancellationToken: cancellationToken);

            var results = searchResults.Select(r => new CustomerSearchResult(
                Guid.Parse(r.Id.Uuid),
                r.Score,
                r.Payload.ToDictionary(
                    p => p.Key,
                    p => (object)(p.Value.StringValue ?? p.Value.IntegerValue.ToString()))
            )).ToList();

            _logger.LogDebug("Found {Count} similar customers", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar customers");
            throw;
        }
    }

    public async Task DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete using Guid IDs
            await _client.DeleteAsync(
                _collectionName,
                ids: new[] { orderId },
                cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted order {OrderId} from collection {Collection}", orderId, _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order {OrderId}", orderId);
            throw;
        }
    }

    public async Task EnsureCustomersCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            if (collections.Any(c => c == _customersCollectionName))
            {
                _logger.LogDebug("Collection {Collection} already exists", _customersCollectionName);
                return;
            }

            await _client.CreateCollectionAsync(
                _customersCollectionName,
                new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created collection {Collection} with vector size {Size}", _customersCollectionName, VectorSize);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("AlreadyExists", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Collection {Collection} already exists (created by concurrent consumer)", _customersCollectionName);
                return;
            }
            _logger.LogError(ex, "Error ensuring customers collection exists");
            throw;
        }
    }

    public async Task UpsertCustomerAsync(Guid customerId, float[] embedding, Dictionary<string, object> payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCustomersCollectionExistsAsync(cancellationToken);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = customerId.ToString() },
                Vectors = embedding
            };

            foreach (var (key, value) in payload)
            {
                point.Payload[key] = value switch
                {
                    string s => s,
                    int i => i,
                    long l => l,
                    double d => d,
                    bool b => b,
                    DateTime dt => dt.ToString("O"),
                    Guid g => g.ToString(),
                    _ => value.ToString() ?? string.Empty
                };
            }

            await _client.UpsertAsync(_customersCollectionName, [point], cancellationToken: cancellationToken);
            _logger.LogDebug("Upserted customer {CustomerId} to collection {Collection}", customerId, _customersCollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting customer {CustomerId}", customerId);
            throw;
        }
    }

    public async Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteAsync(
                _customersCollectionName,
                ids: new[] { customerId },
                cancellationToken: cancellationToken);
            _logger.LogDebug("Deleted customer {CustomerId} from collection {Collection}", customerId, _customersCollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer {CustomerId}", customerId);
            throw;
        }
    }
}
