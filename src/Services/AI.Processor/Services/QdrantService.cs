using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AI.Processor.Services;

public class QdrantService : IQdrantService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly ILogger<QdrantService> _logger;
    private const int VectorSize = 768; // nomic-embed-text dimension

    public QdrantService(IConfiguration configuration, ILogger<QdrantService> logger)
    {
        _logger = logger;
        var host = configuration["Qdrant:Host"] ?? "localhost";
        var port = int.Parse(configuration["Qdrant:Port"] ?? "6334");
        _collectionName = configuration["Qdrant:CollectionName"] ?? "orders";

        _client = new QdrantClient(host, port);
        _logger.LogInformation("QdrantService initialized with endpoint {Host}:{Port}, collection {Collection}", 
            host, port, _collectionName);
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
}
