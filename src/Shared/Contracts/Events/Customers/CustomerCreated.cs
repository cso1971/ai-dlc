namespace Contracts.Events.Customers;

public record CustomerCreated
{
    public Guid CustomerId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
