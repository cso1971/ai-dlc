namespace Contracts.Events.Invoicing;

public record InvoiceGenerated
{
    public Guid InvoiceId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
    public DateTime GeneratedAt { get; init; }
}
