namespace Contracts.Events.Ordering;

public record OrderCompleted
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? InvoiceId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CompletedAt { get; init; }
}
