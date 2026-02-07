namespace AI.Processor.Services;

public interface IQdrantService
{
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);
    Task UpsertOrderAsync(Guid orderId, float[] embedding, Dictionary<string, object> payload, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResult>> SearchSimilarOrdersAsync(float[] embedding, int limit = 10, CancellationToken cancellationToken = default);
    Task DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task EnsureCustomersCollectionExistsAsync(CancellationToken cancellationToken = default);
    Task UpsertCustomerAsync(Guid customerId, float[] embedding, Dictionary<string, object> payload, CancellationToken cancellationToken = default);
}

public record SearchResult(Guid OrderId, float Score, Dictionary<string, object> Payload);
