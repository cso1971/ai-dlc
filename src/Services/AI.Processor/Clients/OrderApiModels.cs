using Contracts.Enums;

namespace AI.Processor.Clients;

public record OrderResponse
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerReference { get; init; }
    public DateOnly? RequestedDeliveryDate { get; init; }
    public int Priority { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string? PaymentTerms { get; init; }
    public string? ShippingMethod { get; init; }
    public ShippingAddressDto? ShippingAddress { get; init; }
    public string? Notes { get; init; }
    public OrderStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? TrackingNumber { get; init; }
    public string? Carrier { get; init; }
    public DateOnly? EstimatedDeliveryDate { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public string? ReceivedBy { get; init; }
    public string? DeliveryNotes { get; init; }
    public Guid? InvoiceId { get; init; }
    public DateTime? InvoicedAt { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime? CancelledAt { get; init; }
    public List<OrderLineResponse> Lines { get; init; } = [];
    public decimal Subtotal { get; init; }
    public decimal TotalTax { get; init; }
    public decimal GrandTotal { get; init; }

    /// <summary>
    /// Generates a comprehensive text representation of the order for embedding
    /// </summary>
    public string ToTextForEmbedding()
    {
        var lines = Lines.Select(l => 
            $"- {l.Quantity} x {l.ProductCode} ({l.Description}): {l.LineTotalWithTax:F2} {CurrencyCode}");
        
        return $"""
            Order {Id}
            Customer: {CustomerId} (Ref: {CustomerReference ?? "N/A"})
            Status: {Status}
            Created: {CreatedAt:yyyy-MM-dd HH:mm}
            Priority: {Priority}
            Currency: {CurrencyCode}
            Payment Terms: {PaymentTerms ?? "N/A"}
            Shipping Method: {ShippingMethod ?? "N/A"}
            
            Shipping Address:
            {ShippingAddress?.RecipientName ?? "N/A"}
            {ShippingAddress?.AddressLine1 ?? ""} {ShippingAddress?.AddressLine2 ?? ""}
            {ShippingAddress?.City ?? ""}, {ShippingAddress?.StateOrProvince ?? ""} {ShippingAddress?.PostalCode ?? ""}
            {ShippingAddress?.CountryCode ?? ""}
            Phone: {ShippingAddress?.PhoneNumber ?? "N/A"}
            
            Order Lines ({Lines.Count} items):
            {string.Join("\n", lines)}
            
            Subtotal: {Subtotal:F2} {CurrencyCode}
            Tax: {TotalTax:F2} {CurrencyCode}
            Grand Total: {GrandTotal:F2} {CurrencyCode}
            
            Tracking: {TrackingNumber ?? "N/A"} ({Carrier ?? "N/A"})
            Notes: {Notes ?? "None"}
            """;
    }
}

public record ShippingAddressDto
{
    public string RecipientName { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string? StateOrProvince { get; init; }
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Notes { get; init; }
}

public record OrderLineResponse
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal LineTotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal LineTotalWithTax { get; init; }
}
