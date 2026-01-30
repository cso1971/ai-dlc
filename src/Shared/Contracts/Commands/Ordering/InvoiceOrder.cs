namespace Contracts.Commands.Ordering;

// Delivered → Invoiced
public record InvoiceOrder
{
    public Guid OrderId { get; init; }
    public Guid? InvoiceId { get; init; }
}
