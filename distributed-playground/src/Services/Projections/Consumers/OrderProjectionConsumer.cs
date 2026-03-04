using Contracts.Events.Ordering;
using MassTransit;
using Projections.Clients;
using Projections.Services;

namespace Projections.Consumers;

public class OrderProjectionConsumer :
    IConsumer<OrderCreated>,
    IConsumer<OrderStatusChanged>,
    IConsumer<OrderShipped>,
    IConsumer<OrderDelivered>,
    IConsumer<OrderCancelled>,
    IConsumer<OrderCompleted>
{
    private readonly IRedisProjectionService _projection;
    private readonly IOrderApiClient _orderApi;
    private readonly ILogger<OrderProjectionConsumer> _logger;

    public OrderProjectionConsumer(
        IRedisProjectionService projection,
        IOrderApiClient orderApi,
        ILogger<OrderProjectionConsumer> logger)
    {
        _projection = projection;
        _orderApi = orderApi;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Projecting OrderCreated {OrderId}", msg.OrderId);

        var order = await _orderApi.GetOrderAsync(msg.OrderId, context.CancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Could not fetch order {OrderId}, skipping projection", msg.OrderId);
            return;
        }

        var snapshot = new OrderSnapshot
        {
            Status = "Created",
            CustomerReference = order.CustomerReference ?? "",
            ShippingMethod = order.ShippingMethod ?? "",
            CurrencyCode = order.CurrencyCode,
            Subtotal = order.Subtotal,
            GrandTotal = order.GrandTotal,
            CreatedAtMonth = order.CreatedAt.ToString("yyyy-MM"),
            CreatedAtYear = order.CreatedAt.ToString("yyyy"),
            LineDescriptions = order.Lines.Select(l => l.Description).Distinct().ToList()
        };

        await _projection.SaveOrderSnapshotAsync(msg.OrderId, snapshot, context.CancellationToken);
        await _projection.IncrementOrderCountAsync(context.CancellationToken);
        await IncrementAllDimensions(snapshot, context.CancellationToken);
        await _projection.SetLastUpdatedAsync(context.CancellationToken);

        _logger.LogInformation("Projected OrderCreated {OrderId}: {GrandTotal} {Currency}",
            msg.OrderId, order.GrandTotal, order.CurrencyCode);
    }

    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Projecting OrderStatusChanged {OrderId}: {Prev} -> {New}",
            msg.OrderId, msg.PreviousStatus, msg.NewStatus);

        var snapshot = await _projection.GetOrderSnapshotAsync(msg.OrderId, context.CancellationToken);
        if (snapshot == null)
        {
            _logger.LogWarning("No snapshot for order {OrderId}, skipping status change", msg.OrderId);
            return;
        }

        var prevStatus = msg.PreviousStatus.ToString();
        var newStatus = msg.NewStatus.ToString();

        await _projection.DecrementDimensionAsync("status", prevStatus, snapshot.Subtotal, snapshot.GrandTotal, context.CancellationToken);
        await _projection.IncrementDimensionAsync("status", newStatus, snapshot.Subtotal, snapshot.GrandTotal, context.CancellationToken);
        await _projection.UpdateSnapshotStatusAsync(msg.OrderId, newStatus, context.CancellationToken);
        await _projection.SetLastUpdatedAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        _logger.LogInformation("Projecting OrderShipped {OrderId}", context.Message.OrderId);
        await _projection.SetLastUpdatedAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderDelivered> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Projecting OrderDelivered {OrderId}", msg.OrderId);

        var snapshot = await _projection.GetOrderSnapshotAsync(msg.OrderId, context.CancellationToken);
        if (snapshot == null) return;

        var month = msg.DeliveredAt.ToString("yyyy-MM");
        var year = msg.DeliveredAt.ToString("yyyy");

        await _projection.IncrementDimensionAsync("delivered-month", month, snapshot.Subtotal, snapshot.GrandTotal, context.CancellationToken);
        await _projection.IncrementDimensionAsync("delivered-year", year, snapshot.Subtotal, snapshot.GrandTotal, context.CancellationToken);
        await _projection.UpdateSnapshotDeliveredAtAsync(msg.OrderId, msg.DeliveredAt, context.CancellationToken);
        await _projection.SetLastUpdatedAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        _logger.LogInformation("Projecting OrderCancelled {OrderId}", context.Message.OrderId);
        await _projection.SetLastUpdatedAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<OrderCompleted> context)
    {
        _logger.LogInformation("Projecting OrderCompleted {OrderId}", context.Message.OrderId);
        await _projection.SetLastUpdatedAsync(context.CancellationToken);
    }

    private async Task IncrementAllDimensions(OrderSnapshot snapshot, CancellationToken ct)
    {
        await _projection.IncrementDimensionAsync("status", snapshot.Status, snapshot.Subtotal, snapshot.GrandTotal, ct);
        await _projection.IncrementDimensionAsync("currency", snapshot.CurrencyCode, snapshot.Subtotal, snapshot.GrandTotal, ct);
        await _projection.IncrementDimensionAsync("customer-ref", snapshot.CustomerReference, snapshot.Subtotal, snapshot.GrandTotal, ct);
        await _projection.IncrementDimensionAsync("shipping-method", snapshot.ShippingMethod, snapshot.Subtotal, snapshot.GrandTotal, ct);
        await _projection.IncrementDimensionAsync("created-month", snapshot.CreatedAtMonth, snapshot.Subtotal, snapshot.GrandTotal, ct);
        await _projection.IncrementDimensionAsync("created-year", snapshot.CreatedAtYear, snapshot.Subtotal, snapshot.GrandTotal, ct);

        foreach (var desc in snapshot.LineDescriptions)
        {
            await _projection.IncrementDimensionAsync("product", desc, snapshot.Subtotal, snapshot.GrandTotal, ct);
        }
    }
}
