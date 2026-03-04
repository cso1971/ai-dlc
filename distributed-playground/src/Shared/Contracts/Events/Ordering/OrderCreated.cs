using Contracts.ValueObjects.Ordering;

namespace Contracts.Events.Ordering;

public record OrderCreated
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerReference { get; init; }
    public string CurrencyCode { get; init; } = "USD";
    public decimal TotalAmount { get; init; }
    public ShippingAddress? ShippingAddress { get; init; }
    public IReadOnlyList<OrderLineItem>? Lines { get; init; }
    public DateTime CreatedAt { get; init; }
}
