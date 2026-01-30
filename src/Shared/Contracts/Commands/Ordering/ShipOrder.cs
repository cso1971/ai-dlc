namespace Contracts.Commands.Ordering;

// InProgress → Shipped
public record ShipOrder
{
    public Guid OrderId { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public string? Carrier { get; init; }
    public DateOnly? EstimatedDeliveryDate { get; init; }
}
