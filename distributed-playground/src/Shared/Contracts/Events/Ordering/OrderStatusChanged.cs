using Contracts.Enums;

namespace Contracts.Events.Ordering;

public record OrderStatusChanged
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public OrderStatus PreviousStatus { get; init; }
    public OrderStatus NewStatus { get; init; }
    public string? Reason { get; init; }
    public DateTime ChangedAt { get; init; }
    public string? ChangedBy { get; init; }
}
