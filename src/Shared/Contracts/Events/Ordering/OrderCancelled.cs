using Contracts.Enums;

namespace Contracts.Events.Ordering;

public record OrderCancelled
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public OrderStatus StatusWhenCancelled { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
    public DateTime CancelledAt { get; init; }
    public string? CancelledBy { get; init; }
}
