namespace Contracts.Events.Customers;

public record CustomerCreated
{
    public Guid CustomerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
