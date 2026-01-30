namespace Contracts.Enums;

public enum OrderStatus
{
    Created = 0,
    InProgress = 1,
    Shipped = 2,
    Delivered = 3,
    Invoiced = 4,
    Cancelled = 99
}
