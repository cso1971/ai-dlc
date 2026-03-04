namespace Contracts.Commands.Ordering;

// Shipped → Delivered
public record DeliverOrder
{
    public Guid OrderId { get; init; }
    public string? ReceivedBy { get; init; }
    public string? DeliveryNotes { get; init; }
}
