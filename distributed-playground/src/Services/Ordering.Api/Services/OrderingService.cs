using Contracts.Commands.Ordering;
using Contracts.Events.Ordering;
using Contracts.Enums;
using MassTransit;
using Ordering.Api.Clients;
using Ordering.Api.Domain;
using Ordering.Api.Endpoints;

namespace Ordering.Api.Services;

public class OrderingService
{
    private readonly IOrderRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ICustomersApiClient _customersApiClient;
    private readonly ILogger<OrderingService> _logger;

    public OrderingService(
        IOrderRepository repository,
        IPublishEndpoint publishEndpoint,
        ICustomersApiClient customersApiClient,
        ILogger<OrderingService> logger)
    {
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _customersApiClient = customersApiClient;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrder command, CancellationToken cancellationToken = default)
    {
        var exists = await _customersApiClient.CustomerExistsAsync(command.CustomerId, cancellationToken);
        if (!exists)
            throw new InvalidOperationException($"Customer {command.CustomerId} not found in Customers context.");

        _logger.LogInformation("Creating order for customer {CustomerId}", command.CustomerId);

        var lines = command.Lines.Select(l => OrderLine.Create(
            l.ProductCode,
            l.Description,
            l.Quantity,
            l.UnitOfMeasure,
            l.UnitPrice,
            l.DiscountPercent,
            l.TaxPercent
        )).ToList();

        var shippingAddress = command.ShippingAddress != null
            ? ShippingAddress.FromContract(command.ShippingAddress)
            : null;

        var order = Order.Create(
            command.CustomerId,
            command.CustomerReference,
            command.RequestedDeliveryDate,
            command.Priority,
            command.CurrencyCode,
            command.PaymentTerms,
            command.ShippingMethod,
            shippingAddress,
            command.Notes,
            lines
        );

        await _repository.AddAsync(order, cancellationToken);

        await _publishEndpoint.Publish(new OrderCreated
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.GrandTotal,
            CreatedAt = order.CreatedAt
        }, cancellationToken);

        _logger.LogInformation("Order {OrderId} created successfully", order.Id);

        return order;
    }

    public async Task<Order> StartProcessingAsync(StartOrderProcessing command, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(command.OrderId, cancellationToken);
        var previousStatus = order.Status;

        order.StartProcessing(command.Notes);

        await _repository.UpdateAsync(order, cancellationToken);

        await PublishStatusChangedAsync(order, previousStatus, cancellationToken);

        _logger.LogInformation("Order {OrderId} processing started", order.Id);

        return order;
    }

    public async Task<Order> ShipOrderAsync(ShipOrder command, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(command.OrderId, cancellationToken);
        var previousStatus = order.Status;

        order.Ship(command.TrackingNumber, command.Carrier, command.EstimatedDeliveryDate);

        await _repository.UpdateAsync(order, cancellationToken);

        await PublishStatusChangedAsync(order, previousStatus, cancellationToken);

        await _publishEndpoint.Publish(new OrderShipped
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TrackingNumber = order.TrackingNumber,
            Carrier = order.Carrier,
            ShippedAt = order.ShippedAt!.Value,
            EstimatedDeliveryDate = order.EstimatedDeliveryDate
        }, cancellationToken);

        _logger.LogInformation("Order {OrderId} shipped with tracking {TrackingNumber}", order.Id, command.TrackingNumber);

        return order;
    }

    public async Task<Order> DeliverOrderAsync(DeliverOrder command, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(command.OrderId, cancellationToken);
        var previousStatus = order.Status;

        order.Deliver(command.ReceivedBy, command.DeliveryNotes);

        await _repository.UpdateAsync(order, cancellationToken);

        await PublishStatusChangedAsync(order, previousStatus, cancellationToken);

        await _publishEndpoint.Publish(new OrderDelivered
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            DeliveredAt = order.DeliveredAt!.Value,
            ReceivedBy = order.ReceivedBy,
            DeliveryNotes = order.DeliveryNotes
        }, cancellationToken);

        _logger.LogInformation("Order {OrderId} delivered", order.Id);

        return order;
    }

    public async Task<Order> InvoiceOrderAsync(InvoiceOrder command, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(command.OrderId, cancellationToken);
        var previousStatus = order.Status;

        order.Invoice(command.InvoiceId);

        await _repository.UpdateAsync(order, cancellationToken);

        await PublishStatusChangedAsync(order, previousStatus, cancellationToken);

        await _publishEndpoint.Publish(new OrderCompleted
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.GrandTotal,
            CompletedAt = order.InvoicedAt!.Value
        }, cancellationToken);

        _logger.LogInformation("Order {OrderId} invoiced", order.Id);

        return order;
    }

    public async Task<Order> CancelOrderAsync(CancelOrder command, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderOrThrowAsync(command.OrderId, cancellationToken);
        var previousStatus = order.Status;

        order.Cancel(command.CancellationReason);

        await _repository.UpdateAsync(order, cancellationToken);

        await PublishStatusChangedAsync(order, previousStatus, cancellationToken);

        await _publishEndpoint.Publish(new OrderCancelled
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            StatusWhenCancelled = previousStatus,
            CancellationReason = order.CancellationReason!,
            CancelledAt = order.CancelledAt!.Value
        }, cancellationToken);

        _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", order.Id, command.CancellationReason);

        return order;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// Returns aggregate stats (total order count, total value per currency) for use by the AI chatbot.
    /// </summary>
    public async Task<OrderStatsResponse> GetOrderStatsAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _repository.GetAllAsync(cancellationToken);
        var byCurrency = orders
            .GroupBy(o => o.CurrencyCode)
            .Select(g => new CurrencyOrderStats
            {
                CurrencyCode = g.Key,
                OrderCount = g.Count(),
                TotalValue = g.Sum(o => o.GrandTotal)
            })
            .OrderBy(c => c.CurrencyCode)
            .ToList();
        var byStatus = orders
            .GroupBy(o => o.Status.ToString())
            .Select(g => new StatusOrderStats
            {
                Status = g.Key,
                OrderCount = g.Count()
            })
            .OrderByDescending(s => s.OrderCount)
            .ToList();
        return new OrderStatsResponse
        {
            TotalOrderCount = orders.Count,
            ByCurrency = byCurrency,
            ByStatus = byStatus
        };
    }

    private async Task<Order> GetOrderOrThrowAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }
        return order;
    }

    private async Task PublishStatusChangedAsync(Order order, OrderStatus previousStatus, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(new OrderStatusChanged
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            PreviousStatus = previousStatus,
            NewStatus = order.Status,
            ChangedAt = order.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);
    }
}
