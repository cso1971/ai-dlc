namespace Contracts.Events.Ordering;

public record OrderShipped
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string? TrackingNumber { get; init; }
    public string? Carrier { get; init; }
    public DateTime ShippedAt { get; init; }
    public DateOnly? EstimatedDeliveryDate { get; init; }
}
