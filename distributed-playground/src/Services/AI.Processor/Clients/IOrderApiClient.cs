namespace AI.Processor.Clients;

public interface IOrderApiClient
{
    Task<OrderResponse?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderStatsResponse?> GetOrderStatsAsync(CancellationToken cancellationToken = default);
}
