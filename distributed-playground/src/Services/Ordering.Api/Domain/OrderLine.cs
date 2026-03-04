namespace Ordering.Api.Domain;

public class OrderLine
{
    public Guid Id { get; private set; }
    public int LineNumber { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = "PCS";
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal TaxPercent { get; private set; }
    
    public decimal LineTotal => Quantity * UnitPrice * (1 - DiscountPercent / 100);
    public decimal TaxAmount => LineTotal * (TaxPercent / 100);
    public decimal LineTotalWithTax => LineTotal + TaxAmount;
    
    private OrderLine() { } // For EF Core
    
    public static OrderLine Create(
        string productCode,
        string description,
        decimal quantity,
        string unitOfMeasure,
        decimal unitPrice,
        decimal discountPercent = 0,
        decimal taxPercent = 0)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            throw new ArgumentException("Product code is required.", nameof(productCode));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        
        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        
        if (discountPercent < 0 || discountPercent > 100)
            throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(discountPercent));
        
        if (taxPercent < 0)
            throw new ArgumentException("Tax percent cannot be negative.", nameof(taxPercent));
        
        return new OrderLine
        {
            Id = Guid.NewGuid(),
            ProductCode = productCode,
            Description = description,
            Quantity = quantity,
            UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? "PCS" : unitOfMeasure,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            TaxPercent = taxPercent
        };
    }
    
    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }
    
    public void UpdateQuantity(decimal newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(newQuantity));
        
        Quantity = newQuantity;
    }
    
    public void UpdatePrice(decimal newUnitPrice, decimal? newDiscountPercent = null)
    {
        if (newUnitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(newUnitPrice));
        
        UnitPrice = newUnitPrice;
        
        if (newDiscountPercent.HasValue)
        {
            if (newDiscountPercent < 0 || newDiscountPercent > 100)
                throw new ArgumentException("Discount percent must be between 0 and 100.", nameof(newDiscountPercent));
            
            DiscountPercent = newDiscountPercent.Value;
        }
    }
}
