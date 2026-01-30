namespace Contracts.Events.Ordering;

public record OrderDelivered
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public DateTime DeliveredAt { get; init; }
    public string? ReceivedBy { get; init; }
    public string? DeliveryNotes { get; init; }
}
