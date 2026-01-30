namespace Contracts.Commands.Invoicing;

public record GenerateInvoice
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
}
