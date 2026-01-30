using Contracts.Enums;
using Contracts.ValueObjects.Ordering;

namespace Ordering.Api.Endpoints;

// Request DTOs
public record CreateOrderRequest
{
    public Guid CustomerId { get; init; }
    public string? CustomerReference { get; init; }
    public DateOnly? RequestedDeliveryDate { get; init; }
    public int Priority { get; init; } = 3;
    public string CurrencyCode { get; init; } = "EUR";
    public string? PaymentTerms { get; init; }
    public string? ShippingMethod { get; init; }
    public ShippingAddressDto? ShippingAddress { get; init; }
    public string? Notes { get; init; }
    public List<OrderLineRequest> Lines { get; init; } = [];
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

public record OrderLineRequest
{
    public string ProductCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string UnitOfMeasure { get; init; } = "PCS";
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
}

public record StartProcessingRequest
{
    public string? Notes { get; init; }
}

public record ShipOrderRequest
{
    public string TrackingNumber { get; init; } = string.Empty;
    public string? Carrier { get; init; }
    public DateOnly? EstimatedDeliveryDate { get; init; }
}

public record DeliverOrderRequest
{
    public string? ReceivedBy { get; init; }
    public string? DeliveryNotes { get; init; }
}

public record InvoiceOrderRequest
{
    public Guid? InvoiceId { get; init; }
}

public record CancelOrderRequest
{
    public string CancellationReason { get; init; } = string.Empty;
}

// Response DTOs
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

public record OrderSummaryResponse
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerReference { get; init; }
    public OrderStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal GrandTotal { get; init; }
    public int LineCount { get; init; }
}
