namespace Contracts.ValueObjects.Ordering;

public record OrderLineItem
{
    public int LineNumber { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string UnitOfMeasure { get; init; } = "PCS";
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
    
    public decimal LineTotal => Quantity * UnitPrice * (1 - DiscountPercent / 100);
    public decimal TaxAmount => LineTotal * (TaxPercent / 100);
    public decimal LineTotalWithTax => LineTotal + TaxAmount;
}
