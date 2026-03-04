namespace Contracts.Events.Customers;

public record CustomerCancelled
{
    public Guid CustomerId { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
    public DateTime CancelledAt { get; init; }
}
