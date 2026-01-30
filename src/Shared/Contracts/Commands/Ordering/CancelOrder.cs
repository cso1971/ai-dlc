namespace Contracts.Commands.Ordering;

public record CancelOrder
{
    public Guid OrderId { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
}
