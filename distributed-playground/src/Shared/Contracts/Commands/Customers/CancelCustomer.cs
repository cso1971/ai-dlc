namespace Contracts.Commands.Customers;

public record CancelCustomer
{
    public Guid CustomerId { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
}
