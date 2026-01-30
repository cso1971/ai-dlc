using Contracts.ValueObjects.Ordering;

namespace Contracts.Commands.Ordering;

public record CreateOrder
{
    // Header
    public Guid CustomerId { get; init; }
    public string? CustomerReference { get; init; }
    public DateOnly? RequestedDeliveryDate { get; init; }
    public int Priority { get; init; } = 3;
    public string CurrencyCode { get; init; } = "EUR";
    public string? PaymentTerms { get; init; }
    public string? ShippingMethod { get; init; }
    public ShippingAddress? ShippingAddress { get; init; }
    public string? Notes { get; init; }
    
    // Lines
    public IReadOnlyList<OrderLineItem> Lines { get; init; } = [];
    
    // Calculated Totals
    public decimal Subtotal => Lines.Sum(l => l.LineTotal);
    public decimal TotalTax => Lines.Sum(l => l.TaxAmount);
    public decimal GrandTotal => Subtotal + TotalTax;
}
