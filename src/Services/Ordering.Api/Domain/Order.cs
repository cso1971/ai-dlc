using Contracts.Enums;

namespace Ordering.Api.Domain;

public class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string? CustomerReference { get; private set; }
    public DateOnly? RequestedDeliveryDate { get; private set; }
    public int Priority { get; private set; }
    public string CurrencyCode { get; private set; } = "EUR";
    public string? PaymentTerms { get; private set; }
    public string? ShippingMethod { get; private set; }
    public ShippingAddress? ShippingAddress { get; private set; }
    public string? Notes { get; private set; }
    
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    
    // Shipping info (populated when shipped)
    public string? TrackingNumber { get; private set; }
    public string? Carrier { get; private set; }
    public DateOnly? EstimatedDeliveryDate { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    
    // Delivery info (populated when delivered)
    public DateTime? DeliveredAt { get; private set; }
    public string? ReceivedBy { get; private set; }
    public string? DeliveryNotes { get; private set; }
    
    // Invoice info (populated when invoiced)
    public Guid? InvoiceId { get; private set; }
    public DateTime? InvoicedAt { get; private set; }
    
    // Cancellation info (populated when cancelled)
    public string? CancellationReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    
    // Calculated totals
    public decimal Subtotal => _lines.Sum(l => l.LineTotal);
    public decimal TotalTax => _lines.Sum(l => l.TaxAmount);
    public decimal GrandTotal => Subtotal + TotalTax;
    
    private Order() { } // For EF Core
    
    public static Order Create(
        Guid customerId,
        string? customerReference,
        DateOnly? requestedDeliveryDate,
        int priority,
        string currencyCode,
        string? paymentTerms,
        string? shippingMethod,
        ShippingAddress? shippingAddress,
        string? notes,
        IEnumerable<OrderLine> lines)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CustomerReference = customerReference,
            RequestedDeliveryDate = requestedDeliveryDate,
            Priority = priority,
            CurrencyCode = currencyCode,
            PaymentTerms = paymentTerms,
            ShippingMethod = shippingMethod,
            ShippingAddress = shippingAddress,
            Notes = notes,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow
        };
        
        foreach (var line in lines)
        {
            order._lines.Add(line);
        }
        
        return order;
    }
    
    public void StartProcessing(string? notes = null)
    {
        EnsureStatus(OrderStatus.Created);
        
        Status = OrderStatus.InProgress;
        Notes = string.IsNullOrEmpty(notes) ? Notes : $"{Notes}\n{notes}".Trim();
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Ship(string trackingNumber, string? carrier, DateOnly? estimatedDeliveryDate)
    {
        EnsureStatus(OrderStatus.InProgress);
        
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new InvalidOperationException("Tracking number is required for shipping.");
        
        Status = OrderStatus.Shipped;
        TrackingNumber = trackingNumber;
        Carrier = carrier;
        EstimatedDeliveryDate = estimatedDeliveryDate;
        ShippedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Deliver(string? receivedBy, string? deliveryNotes)
    {
        EnsureStatus(OrderStatus.Shipped);
        
        Status = OrderStatus.Delivered;
        ReceivedBy = receivedBy;
        DeliveryNotes = deliveryNotes;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Invoice(Guid? invoiceId)
    {
        EnsureStatus(OrderStatus.Delivered);
        
        Status = OrderStatus.Invoiced;
        InvoiceId = invoiceId;
        InvoicedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Invoiced)
            throw new InvalidOperationException("Cannot cancel an invoiced order.");
        
        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");
        
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Cancellation reason is required.");
        
        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void AddLine(OrderLine line)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("Cannot modify lines after order processing has started.");
        
        line.SetLineNumber(_lines.Count + 1);
        _lines.Add(line);
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void RemoveLine(int lineNumber)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("Cannot modify lines after order processing has started.");
        
        var line = _lines.FirstOrDefault(l => l.LineNumber == lineNumber);
        if (line != null)
        {
            _lines.Remove(line);
            RenumberLines();
            UpdatedAt = DateTime.UtcNow;
        }
    }
    
    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Order must be in '{expected}' status. Current status: '{Status}'.");
    }
    
    private void RenumberLines()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetLineNumber(i + 1);
        }
    }
}
