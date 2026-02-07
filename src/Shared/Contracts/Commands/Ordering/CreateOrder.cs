using Contracts.ValueObjects.Ordering;

namespace Contracts.Commands.Ordering;

/// <summary>
/// Command to create a new order. The order references a customer in the Customers bounded context via <see cref="CustomerId"/>.
/// </summary>
public record CreateOrder
{
    // Header

    /// <summary>
    /// Reference to the Customer aggregate in the Customers bounded context (same Guid as Customer.Id).
    /// The customer must exist in the Customers context.
    /// </summary>
    public Guid CustomerId { get; init; }

    /// <summary>
    /// Optional order reference from the customer's side (e.g. their PO number, order reference).
    /// Not the same as the customer identity; use <see cref="CustomerId"/> for the reference to the Customer.
    /// </summary>
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
